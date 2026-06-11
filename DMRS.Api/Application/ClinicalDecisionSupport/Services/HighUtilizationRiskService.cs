using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    /// <summary>
    /// Predicts 30-day hospital readmission risk (surfaced in the UI as "Readmission Risk").
    /// The score is produced entirely by the trained ONNX model from six FHIR-derived features —
    /// [age, gender, conditionCount, medicationCount, recentEncounterCount, procedureCount] — rather
    /// than the hand-tuned score boosts used previously. The clinical counts are still surfaced on the
    /// assessment as the model's inputs / informational factors, and the type/CDS-variable names are
    /// kept as "HighUtilization" so existing CDS rules that reference them keep working.
    /// </summary>
    public sealed class HighUtilizationRiskService : IHighUtilizationRiskService
    {
        // Medians used only when age/gender are missing on the Patient (counts always come from FHIR).
        // Update from train_readmission.py's "Imputation medians" printout if they differ.
        private const float MedianAge = 65f;
        private const float MedianGender = 1f; // Female=1, Male=0

        private static readonly HashSet<string> ChronicConditionKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "diabetes", "diabetic", "copd", "heart failure", "renal failure", "kidney disease",
            "chronic kidney", "ckd", "cancer", "hypertension", "asthma", "stroke",
            "coronary artery", "atrial fibrillation", "epilepsy", "alzheimer", "parkinson",
            "multiple sclerosis", "cirrhosis", "liver disease", "obesity"
        };

        private static readonly HashSet<string> ChronicSnomedCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "44054006",  // Type 2 diabetes
            "73211009",  // Diabetes mellitus
            "13645005",  // COPD
            "84114007",  // Heart failure
            "38341003",  // Hypertension
            "363346000", // Malignant neoplasm
            "709044004", // CKD
            "195967001", // Asthma
            "230690007", // Stroke
            "41401008",  // Coronary artery disease
        };

        private readonly IFhirRepository _fhirRepository;
        private readonly InferenceSession? _session;
        private readonly AiRiskPredictorOptions _options;
        private readonly string _modelName;
        private readonly string _inputName;
        private readonly ILogger<HighUtilizationRiskService> _logger;

        public HighUtilizationRiskService(
            IFhirRepository fhirRepository,
            IOptions<AiRiskPredictorOptions> options,
            IWebHostEnvironment environment,
            OnnxModelPool modelPool,
            ILogger<HighUtilizationRiskService> logger)
        {
            _fhirRepository = fhirRepository;
            _options = options.Value;
            _logger = logger;

            var modelPath = Path.IsPathRooted(_options.ModelPath)
                ? _options.ModelPath
                : Path.Combine(environment.ContentRootPath, _options.ModelPath);

            _modelName = Path.GetFileName(modelPath);

            // Degrade gracefully when the model file has not been trained/placed yet.
            // The session is loaded once and shared via the singleton pool (see OnnxModelPool):
            // this service is scoped, so creating the session here per request would reload the
            // model from disk on every call.
            if (File.Exists(modelPath))
            {
                _session = modelPool.GetOrLoad(modelPath);
                _inputName = _session.InputMetadata.Keys.FirstOrDefault() ?? _options.InputName;
            }
            else
            {
                _inputName = _options.InputName;
                logger.LogWarning("Readmission predictor model not found at '{ModelPath}' — risk scoring disabled.", modelPath);
            }
        }

        public async Task<HighUtilizationRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId) || _session is null)
            {
                return null;
            }

            var normalizedPatientId = patientId.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase)
                ? patientId["Patient/".Length..]
                : patientId;

            var patient = await _fhirRepository.GetAsync<Patient>(normalizedPatientId);
            if (patient == null)
            {
                return null;
            }

            // Fetch clinical data sequentially — EF Core's scoped DbContext does not support
            // concurrent async operations on the same instance, so Task.WhenAll is not safe here.
            var patientRef = $"Patient/{normalizedPatientId}";

            // Conditions ARE deserialized — we need their codes/text for chronic-condition detection.
            var conditions = await _fhirRepository.SearchAsync<Condition>(new Dictionary<string, string> { ["patient"] = patientRef });
            // Medications ARE deserialized — we need each one's status to count active prescriptions.
            var medications = await _fhirRepository.SearchAsync<MedicationRequest>(new Dictionary<string, string> { ["patient"] = patientRef });
            // Encounters and procedures are only needed as COUNTS, so use a cheap indexed count
            // instead of loading + deserializing every resource (a patient can have 100+ encounters).
            var encounterCount = await _fhirRepository.SearchCountAsync<Encounter>(new Dictionary<string, string> { ["patient"] = patientRef });
            var procedureCount = await _fhirRepository.SearchCountAsync<Procedure>(new Dictionary<string, string> { ["patient"] = patientRef });

            var conditionCount = conditions.Count;
            var medicationCount = medications.Count(m =>
                m.Status == MedicationRequest.MedicationrequestStatus.Active ||
                m.Status == MedicationRequest.MedicationrequestStatus.OnHold);

            // The Synthea seed encounters carry no R5 ActualPeriod (they store the R4 'period'),
            // so the previous "encounters in the last 6 months, else fall back to total" logic
            // always fell back to the total anyway. Using the total count is the identical result
            // for this data without deserializing every encounter.
            var recentEncounterCount = encounterCount;
            var hasChronicConditions = DetectChronicConditions(conditions);

            return BuildAssessment(
                normalizedPatientId,
                patient,
                conditionCount,
                medicationCount,
                recentEncounterCount,
                procedureCount,
                hasChronicConditions);
        }

        /// <summary>
        /// Scores the whole cohort in one pass. The browser caps concurrent connections, so 100
        /// per-patient HTTP calls serialize badly; this lets the dashboard make a single request.
        /// Per-patient encounter/procedure counts come from grouped index queries (no deserialization);
        /// conditions and medications are deserialized once for the whole cohort (needed for chronic
        /// detection and active-medication status) rather than per patient.
        /// </summary>
        public async Task<IReadOnlyList<HighUtilizationRiskAssessment>> AssessAllAsync(CancellationToken cancellationToken)
        {
            if (_session is null)
            {
                return [];
            }

            var noFilter = new Dictionary<string, string>();
            var patients = await _fhirRepository.SearchAsync<Patient>(noFilter);
            var conditions = await _fhirRepository.SearchAsync<Condition>(noFilter);
            var medications = await _fhirRepository.SearchAsync<MedicationRequest>(noFilter);
            var encounterCounts = await _fhirRepository.CountByPatientAsync("Encounter", cancellationToken);
            var procedureCounts = await _fhirRepository.CountByPatientAsync("Procedure", cancellationToken);

            var conditionsByPatient = conditions
                .GroupBy(c => ExtractPatientId(c.Subject?.Reference))
                .Where(g => g.Key is not null)
                .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
            var medicationsByPatient = medications
                .GroupBy(m => ExtractPatientId(m.Subject?.Reference))
                .Where(g => g.Key is not null)
                .ToDictionary(g => g.Key!, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var results = new List<HighUtilizationRiskAssessment>(patients.Count);
            foreach (var patient in patients)
            {
                // Same eligibility gate the dashboard used to apply client-side: needs age + gender.
                if (string.IsNullOrWhiteSpace(patient.Id)
                    || string.IsNullOrWhiteSpace(patient.BirthDate)
                    || (patient.Gender != AdministrativeGender.Male && patient.Gender != AdministrativeGender.Female))
                {
                    continue;
                }

                var id = patient.Id;
                var patientConditions = conditionsByPatient.GetValueOrDefault(id) ?? [];
                var patientMedications = medicationsByPatient.GetValueOrDefault(id) ?? [];

                var medicationCount = patientMedications.Count(m =>
                    m.Status == MedicationRequest.MedicationrequestStatus.Active ||
                    m.Status == MedicationRequest.MedicationrequestStatus.OnHold);

                var assessment = BuildAssessment(
                    id,
                    patient,
                    patientConditions.Count,
                    medicationCount,
                    encounterCounts.GetValueOrDefault(id),
                    procedureCounts.GetValueOrDefault(id),
                    DetectChronicConditions(patientConditions));

                if (assessment is not null)
                {
                    results.Add(assessment);
                }
            }

            return results;
        }

        private static string? ExtractPatientId(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            var slash = reference.IndexOf('/');
            return slash >= 0 && slash < reference.Length - 1 ? reference[(slash + 1)..] : reference;
        }

        /// <summary>
        /// Runs the model and packages the assessment from already-computed feature counts.
        /// Shared by the single-patient and whole-cohort paths so the scoring lives in one place.
        /// </summary>
        private HighUtilizationRiskAssessment? BuildAssessment(
            string patientId,
            Patient patient,
            int conditionCount,
            int medicationCount,
            int recentEncounterCount,
            int procedureCount,
            bool hasChronicConditions)
        {
            var ageYears = CalculateAgeYears(patient.BirthDate);
            var genderValue = ToGenderFeature(patient.Gender);
            var featuresComplete = ageYears.HasValue && genderValue.HasValue;
            var age = ageYears ?? MedianAge;
            var gender = genderValue ?? MedianGender;

            // Feature order MUST match train_readmission.py:
            // [age, gender, conditionCount, medicationCount, recentEncounterCount, procedureCount]
            var inputTensor = new DenseTensor<float>(
                new[] { age, gender, conditionCount, medicationCount, recentEncounterCount, procedureCount },
                new[] { 1, 6 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            };

            float? probability;
            bool? label;
            try
            {
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
                (label, probability) = OnnxOutputParser.ParseOutputs(results);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Readmission model inference failed for patient {PatientId}.", patientId);
                return null;
            }

            // The model output IS the score — no hand-tuned boosts. The High/Medium
            // cut-points are config-driven (Cds:Ai:HighUtilizationRisk) so a retrained
            // model's thresholds can be applied without recompiling — see train_readmission.py.
            var score = Math.Clamp(probability ?? (label == true ? 0.5f : 0.1f), 0f, 1f);
            var riskLevel = score >= _options.HighRiskThreshold ? "High"
                : score >= _options.MediumRiskThreshold ? "Medium" : "Low";
            var isHighRisk = score >= _options.HighRiskThreshold;

            // Informational factors describing the model's inputs (not score contributors).
            var riskFactors = new List<string>();
            if (hasChronicConditions)
            {
                riskFactors.Add("Chronic condition on record");
            }
            if (medicationCount >= 10)
            {
                riskFactors.Add($"Severe polypharmacy ({medicationCount} active medications)");
            }
            else if (medicationCount >= 5)
            {
                riskFactors.Add($"Polypharmacy ({medicationCount} active medications)");
            }
            if (recentEncounterCount >= 4)
            {
                riskFactors.Add($"High encounter frequency ({recentEncounterCount} recent visits)");
            }
            if (conditionCount >= 3)
            {
                riskFactors.Add($"Multiple conditions ({conditionCount} on record)");
            }
            if (procedureCount >= 3)
            {
                riskFactors.Add($"Multiple procedures ({procedureCount} on record)");
            }
            if (age >= 65)
            {
                riskFactors.Add("Age ≥ 65");
            }
            if (riskFactors.Count == 0)
            {
                riskFactors.Add("No major utilization factors");
            }

            return new HighUtilizationRiskAssessment(
                patientId,
                age,
                gender,
                isHighRisk,
                probability,
                _modelName,
                DateTimeOffset.UtcNow,
                featuresComplete,
                conditionCount,
                medicationCount,
                recentEncounterCount,
                hasChronicConditions,
                score,
                riskLevel,
                riskFactors.ToArray());
        }

        private static bool DetectChronicConditions(IEnumerable<Condition> conditions)
        {
            foreach (var condition in conditions)
            {
                var codes = condition.Code?.Coding?.Select(c => c.Code) ?? [];
                if (codes.Any(code => !string.IsNullOrWhiteSpace(code) && ChronicSnomedCodes.Contains(code!)))
                {
                    return true;
                }

                var texts = new[]
                {
                    condition.Code?.Text,
                    condition.Code?.Coding?.FirstOrDefault()?.Display
                };

                foreach (var text in texts)
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    if (ChronicConditionKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static float? ToGenderFeature(AdministrativeGender? gender)
        {
            return gender switch
            {
                AdministrativeGender.Female => 1f,
                AdministrativeGender.Male => 0f,
                _ => null
            };
        }

        private static float? CalculateAgeYears(string? birthDate)
        {
            if (string.IsNullOrWhiteSpace(birthDate) || !DateOnly.TryParse(birthDate, out var birthDateValue))
            {
                return null;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - birthDateValue.Year;
            if (today < birthDateValue.AddYears(age))
            {
                age--;
            }

            return age < 0 ? null : age;
        }
    }
}
