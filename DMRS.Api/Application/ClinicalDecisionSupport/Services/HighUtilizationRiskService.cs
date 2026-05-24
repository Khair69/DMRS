using System.Collections;
using System.Collections.ObjectModel;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class HighUtilizationRiskService : IHighUtilizationRiskService, IDisposable
    {
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
        private readonly InferenceSession _session;
        private readonly AiRiskPredictorOptions _options;
        private readonly string _modelName;

        public HighUtilizationRiskService(
            IFhirRepository fhirRepository,
            IOptions<AiRiskPredictorOptions> options,
            IWebHostEnvironment environment)
        {
            _fhirRepository = fhirRepository;
            _options = options.Value;

            var modelPath = Path.IsPathRooted(_options.ModelPath)
                ? _options.ModelPath
                : Path.Combine(environment.ContentRootPath, _options.ModelPath);

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"High-risk predictor model not found at '{modelPath}'.", modelPath);
            }

            _session = new InferenceSession(modelPath);
            _modelName = Path.GetFileName(modelPath);
        }

        public async Task<HighUtilizationRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId))
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

            var ageYears = CalculateAgeYears(patient.BirthDate);
            var genderValue = ToGenderFeature(patient.Gender);

            if (ageYears == null || genderValue == null)
            {
                return new HighUtilizationRiskAssessment(
                    normalizedPatientId,
                    ageYears ?? 0,
                    genderValue ?? 0,
                    false,
                    null,
                    _modelName,
                    DateTimeOffset.UtcNow,
                    false);
            }

            // Run ONNX model
            var inputTensor = new DenseTensor<float>(new[] { ageYears.Value, genderValue.Value }, new[] { 1, 2 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_options.InputName, inputTensor)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
            var (label, probability) = ParseOutputs(results);
            var modelProbability = probability ?? (label == true ? 1f : 0f);

            // Fetch clinical data in parallel for composite scoring
            var patientRef = $"Patient/{normalizedPatientId}";
            var conditionsTask = _fhirRepository.SearchAsync<Condition>(new Dictionary<string, string> { ["patient"] = patientRef });
            var medicationsTask = _fhirRepository.SearchAsync<MedicationRequest>(new Dictionary<string, string> { ["patient"] = patientRef });
            var encountersTask = _fhirRepository.SearchAsync<Encounter>(new Dictionary<string, string> { ["patient"] = patientRef });

            await System.Threading.Tasks.Task.WhenAll(conditionsTask, medicationsTask, encountersTask);

            var conditions = conditionsTask.Result;
            var medications = medicationsTask.Result;
            var encounters = encountersTask.Result;

            var conditionCount = conditions.Count;
            var medicationCount = medications.Count(m =>
                m.Status == MedicationRequest.MedicationrequestStatus.Active ||
                m.Status == MedicationRequest.MedicationrequestStatus.OnHold);

            var sixMonthsAgo = DateTimeOffset.UtcNow.AddMonths(-6);
            var recentEncounterCount = encounters.Count(e =>
            {
                // FHIR R5 uses ActualPeriod; fall back to counting all if period not set
                var start = e.ActualPeriod?.Start;
                return start != null && DateTimeOffset.TryParse(start, out var dt) && dt >= sixMonthsAgo;
            });
            if (recentEncounterCount == 0)
            {
                // Fallback: count all encounters as a proxy when period data is absent
                recentEncounterCount = encounters.Count;
            }

            var hasChronicConditions = DetectChronicConditions(conditions);

            // Build composite score: ONNX base + clinical signal boosts
            var composite = modelProbability;
            var riskFactors = new List<string>();

            if (hasChronicConditions)
            {
                composite += 0.18f;
                riskFactors.Add("Chronic condition on record");
            }
            if (medicationCount >= 5)
            {
                composite += 0.12f;
                riskFactors.Add($"Polypharmacy ({medicationCount} active medications)");
            }
            else if (medicationCount >= 3)
            {
                composite += 0.05f;
            }
            if (recentEncounterCount >= 4)
            {
                composite += 0.12f;
                riskFactors.Add($"High encounter frequency ({recentEncounterCount} recent visits)");
            }
            else if (recentEncounterCount >= 2)
            {
                composite += 0.05f;
            }
            if (conditionCount >= 3)
            {
                composite += 0.06f;
                riskFactors.Add($"Multiple conditions ({conditionCount} on record)");
            }
            if (ageYears.Value >= 65)
            {
                composite += 0.08f;
                riskFactors.Add("Age ≥ 65");
            }
            else if (ageYears.Value >= 50)
            {
                composite += 0.03f;
            }

            composite = Math.Clamp(composite, 0f, 1f);

            var riskLevel = composite >= 0.65f ? "High" : composite >= 0.35f ? "Medium" : "Low";
            var isHighRisk = composite >= _options.HighRiskThreshold;

            if (riskFactors.Count == 0)
            {
                riskFactors.Add("Demographic baseline only");
            }

            return new HighUtilizationRiskAssessment(
                normalizedPatientId,
                ageYears.Value,
                genderValue.Value,
                isHighRisk,
                probability,
                _modelName,
                DateTimeOffset.UtcNow,
                true,
                conditionCount,
                medicationCount,
                recentEncounterCount,
                hasChronicConditions,
                composite,
                riskLevel,
                riskFactors.ToArray());
        }

        private static bool DetectChronicConditions(IEnumerable<Condition> conditions)
        {
            foreach (var condition in conditions)
            {
                // Check coding codes
                var codes = condition.Code?.Coding?.Select(c => c.Code) ?? [];
                if (codes.Any(code => !string.IsNullOrWhiteSpace(code) && ChronicSnomedCodes.Contains(code!)))
                {
                    return true;
                }

                // Check display text and condition text
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

        public void Dispose()
        {
            _session.Dispose();
        }

        private static (bool? label, float? probability) ParseOutputs(IEnumerable<DisposableNamedOnnxValue> outputs)
        {
            bool? label = null;
            float? probability = null;

            foreach (var output in outputs)
            {
                if (TryReadLabel(output, out var parsedLabel))
                {
                    label ??= parsedLabel;
                }

                if (TryReadProbability(output, out var parsedProbability))
                {
                    probability ??= parsedProbability;
                }
            }

            return (label, probability);
        }

        private static bool TryReadLabel(DisposableNamedOnnxValue output, out bool label)
        {
            label = false;

            try
            {
                if (output.AsTensor<long>() is Tensor<long> longTensor && longTensor.Length > 0)
                {
                    label = longTensor.ToArray()[0] != 0;
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                if (output.AsTensor<int>() is Tensor<int> intTensor && intTensor.Length > 0)
                {
                    label = intTensor.ToArray()[0] != 0;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryReadProbability(DisposableNamedOnnxValue output, out float probability)
        {
            probability = 0;

            try
            {
                if (output.AsTensor<float>() is Tensor<float> floatTensor && floatTensor.Length > 0)
                {
                    var values = floatTensor.ToArray();
                    probability = values.Length == 1 ? values[0] : values[^1];
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var enumerable = output.AsEnumerable<Dictionary<long, float>>();
                var map = enumerable.FirstOrDefault();
                if (map != null)
                {
                    probability = map.TryGetValue(1, out var positiveProbability)
                        ? positiveProbability
                        : map.Values.DefaultIfEmpty(0).Max();
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var enumerable = output.AsEnumerable<Dictionary<int, float>>();
                var map = enumerable.FirstOrDefault();
                if (map != null)
                {
                    probability = map.TryGetValue(1, out var positiveProbability)
                        ? positiveProbability
                        : map.Values.DefaultIfEmpty(0).Max();
                    return true;
                }
            }
            catch
            {
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
