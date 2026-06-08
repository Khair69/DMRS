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

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
            var (label, probability) = OnnxOutputParser.ParseOutputs(results);

            var score = probability ?? (label == true ? 1f : 0f);
            var isHighRisk = score >= _options.HighRiskThreshold;
            var riskLevel = score >= 0.65f ? "High" : score >= 0.35f ? "Medium" : "Low";

            return new DiabetesRiskAssessment(
                normalizedPatientId,
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
