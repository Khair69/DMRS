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
    /// Diabetes risk predictor. Builds the model's 4-feature vector
    /// [Glucose, BloodPressure(diastolic), BMI, Age] from the patient's FHIR Observations, imputing
    /// the training-set median for any feature the patient lacks, then runs the ONNX classifier.
    /// </summary>
    public sealed class DiabetesRiskService : IDiabetesRiskService, IDisposable
    {
        // Training-set medians (from train_diabetes.py). Update if the notebook prints different values.
        private const float MedianGlucose = 117f;
        private const float MedianBloodPressure = 72f;
        private const float MedianBmi = 32.3f;
        private const float MedianAge = 29f;

        // LOINC codes for blood glucose (blood, serum, fasting, capillary).
        private static readonly string[] GlucoseCodes = { "2339-0", "2345-7", "1558-6", "41653-7" };
        private const string DiastolicBpCode = "8462-4";
        private const string BmiCode = "39156-5";

        private readonly IFhirRepository _fhirRepository;
        private readonly ObservationFeatureExtractor _extractor;
        private readonly InferenceSession? _session;
        private readonly DiabetesRiskPredictorOptions _options;
        private readonly string _modelName;
        private readonly string _inputName;

        public DiabetesRiskService(
            IFhirRepository fhirRepository,
            ObservationFeatureExtractor extractor,
            IOptions<DiabetesRiskPredictorOptions> options,
            IWebHostEnvironment environment,
            ILogger<DiabetesRiskService> logger)
        {
            _fhirRepository = fhirRepository;
            _extractor = extractor;
            _options = options.Value;

            var modelPath = Path.IsPathRooted(_options.ModelPath)
                ? _options.ModelPath
                : Path.Combine(environment.ContentRootPath, _options.ModelPath);

            _modelName = Path.GetFileName(modelPath);

            // Degrade gracefully when the model file has not been trained/placed yet: the service
            // simply returns null assessments rather than failing requests (and the patient chart).
            if (File.Exists(modelPath))
            {
                _session = new InferenceSession(modelPath);
                // Use the model's actual input name rather than assuming "float_input" — different
                // skl2onnx versions name it "X". Falls back to the configured name if unavailable.
                _inputName = _session.InputMetadata.Keys.FirstOrDefault() ?? _options.InputName;
            }
            else
            {
                _inputName = _options.InputName;
                logger.LogWarning("Diabetes predictor model not found at '{ModelPath}' — risk scoring disabled.", modelPath);
            }
        }

        public async Task<DiabetesRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken)
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

            var observations = await _extractor.GetObservationsAsync(normalizedPatientId);
            return BuildAssessment(normalizedPatientId, patient, observations);
        }

        /// <summary>
        /// Scores every patient with a birth date in a single pass. All Observations are loaded once
        /// and grouped by patient (rather than one search per patient), then each patient's feature
        /// vector is built and run through the model — mirroring the readmission cohort path.
        /// </summary>
        public async Task<IReadOnlyList<DiabetesRiskAssessment>> AssessAllAsync(IReadOnlyCollection<string>? patientIdFilter, CancellationToken cancellationToken)
        {
            if (_session is null)
            {
                return [];
            }

            // A non-null filter scopes scoring to a caller's accessible patients; an empty filter
            // means there is nothing to score.
            var patientIdSet = patientIdFilter is null
                ? null
                : patientIdFilter.ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (patientIdSet is { Count: 0 })
            {
                return [];
            }

            var noFilter = new Dictionary<string, string>();
            var patients = await _fhirRepository.SearchAsync<Patient>(noFilter);
            if (patientIdSet is not null)
            {
                patients = patients.Where(p => p.Id is not null && patientIdSet.Contains(p.Id)).ToList();
            }
            var observations = await _fhirRepository.SearchAsync<Observation>(noFilter);

            var observationsByPatient = observations
                .GroupBy(o => ExtractPatientId(o.Subject?.Reference))
                .Where(g => g.Key is not null)
                .ToDictionary(g => g.Key!, g => (IReadOnlyList<Observation>)g.ToList(), StringComparer.OrdinalIgnoreCase);

            var results = new List<DiabetesRiskAssessment>(patients.Count);
            foreach (var patient in patients)
            {
                // Age is the only patient-derived feature, so a birth date is the minimum needed to
                // produce a non-fully-imputed score; patients without one are skipped.
                if (string.IsNullOrWhiteSpace(patient.Id) || string.IsNullOrWhiteSpace(patient.BirthDate))
                {
                    continue;
                }

                var patientObservations = observationsByPatient.GetValueOrDefault(patient.Id) ?? [];
                var assessment = BuildAssessment(patient.Id, patient, patientObservations);
                if (assessment is not null)
                {
                    results.Add(assessment);
                }
            }

            return results;
        }

        /// <summary>
        /// Builds the 4-feature vector from the patient's Observations (imputing the training median for
        /// any missing feature) and runs the model. Shared by the single-patient and cohort paths.
        /// </summary>
        private DiabetesRiskAssessment? BuildAssessment(string patientId, Patient patient, IReadOnlyList<Observation> observations)
        {
            var imputed = new List<string>();

            var glucose = Resolve(ObservationFeatureExtractor.LatestValue(observations, GlucoseCodes), MedianGlucose, "Glucose", imputed);
            var bloodPressure = Resolve(ObservationFeatureExtractor.LatestValue(observations, DiastolicBpCode), MedianBloodPressure, "BloodPressure", imputed);
            var bmi = Resolve(ObservationFeatureExtractor.LatestValue(observations, BmiCode), MedianBmi, "BMI", imputed);

            var ageYears = CalculateAgeYears(patient.BirthDate);
            var age = ageYears ?? MedianAge;
            if (ageYears == null)
            {
                imputed.Add("Age");
            }

            var inputTensor = new DenseTensor<float>(new[] { glucose, bloodPressure, bmi, age }, new[] { 1, 4 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            };

            float? probability;
            bool? label;
            try
            {
                using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session!.Run(inputs);
                (label, probability) = OnnxOutputParser.ParseOutputs(results);
            }
            catch
            {
                // One bad patient must not fail the whole cohort scoring request.
                return null;
            }

            var score = probability ?? (label == true ? 1f : 0f);
            var isHighRisk = score >= _options.HighRiskThreshold;
            var riskLevel = score >= 0.65f ? "High" : score >= 0.35f ? "Medium" : "Low";

            return new DiabetesRiskAssessment(
                patientId,
                glucose,
                bloodPressure,
                bmi,
                age,
                isHighRisk,
                probability,
                riskLevel,
                imputed.Count == 0,
                imputed.ToArray(),
                _modelName,
                DateTimeOffset.UtcNow);
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

        private static float Resolve(double? value, float median, string name, List<string> imputed)
        {
            if (value.HasValue)
            {
                return (float)value.Value;
            }

            imputed.Add(name);
            return median;
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

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
