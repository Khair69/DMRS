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
    /// Cardiovascular (heart disease) risk predictor. Builds the model's 6-feature vector
    /// [age, sex, trestbps, chol, thalach, fbs] from the patient's FHIR data, imputing the
    /// training-set median for any feature the patient lacks, then runs the ONNX classifier.
    /// Note: sex is encoded male=1 / female=0 to match the UCI Heart Disease dataset (the opposite
    /// of the high-utilization model's encoding).
    /// </summary>
    public sealed class CardiovascularRiskService : ICardiovascularRiskService, IDisposable
    {
        // Training-set medians (from train_cardiovascular.py). Update if the notebook prints different values.
        private const float MedianAge = 56f;
        private const float MedianSex = 1f;
        private const float MedianRestingBp = 130f;
        private const float MedianCholesterol = 240f;
        private const float MedianMaxHeartRate = 152f;
        private const float MedianFastingBloodSugar = 0f;

        private const string SystolicBpCode = "8480-6";
        private const string TotalCholesterolCode = "2093-3";
        private const string HeartRateCode = "8867-4";
        private static readonly string[] GlucoseCodes = { "2339-0", "2345-7", "1558-6", "41653-7" };

        private readonly IFhirRepository _fhirRepository;
        private readonly ObservationFeatureExtractor _extractor;
        private readonly InferenceSession? _session;
        private readonly CardiovascularRiskPredictorOptions _options;
        private readonly string _modelName;
        private readonly string _inputName;

        public CardiovascularRiskService(
            IFhirRepository fhirRepository,
            ObservationFeatureExtractor extractor,
            IOptions<CardiovascularRiskPredictorOptions> options,
            IWebHostEnvironment environment,
            ILogger<CardiovascularRiskService> logger)
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
                logger.LogWarning("Cardiovascular predictor model not found at '{ModelPath}' — risk scoring disabled.", modelPath);
            }
        }

        public async Task<CardiovascularRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken)
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
        public async Task<IReadOnlyList<CardiovascularRiskAssessment>> AssessAllAsync(IReadOnlyCollection<string>? patientIdFilter, CancellationToken cancellationToken)
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

            var results = new List<CardiovascularRiskAssessment>(patients.Count);
            foreach (var patient in patients)
            {
                // Need a birth date for a real age feature; sex is imputed when gender is unset.
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
        /// Builds the 6-feature vector from the patient's data (imputing the training median for any
        /// missing feature) and runs the model. Shared by the single-patient and cohort paths.
        /// </summary>
        private CardiovascularRiskAssessment? BuildAssessment(string patientId, Patient patient, IReadOnlyList<Observation> observations)
        {
            var imputed = new List<string>();

            var ageYears = CalculateAgeYears(patient.BirthDate);
            var age = ageYears ?? MedianAge;
            if (ageYears == null)
            {
                imputed.Add("age");
            }

            var sexValue = ToSexFeature(patient.Gender);
            var sex = sexValue ?? MedianSex;
            if (sexValue == null)
            {
                imputed.Add("sex");
            }

            var restingBp = Resolve(ObservationFeatureExtractor.LatestValue(observations, SystolicBpCode), MedianRestingBp, "trestbps", imputed);
            var cholesterol = Resolve(ObservationFeatureExtractor.LatestValue(observations, TotalCholesterolCode), MedianCholesterol, "chol", imputed);
            var maxHeartRate = Resolve(ObservationFeatureExtractor.LatestValue(observations, HeartRateCode), MedianMaxHeartRate, "thalach", imputed);

            // fbs = fasting blood sugar > 120 mg/dL (1/0), derived from the latest glucose Observation.
            var glucose = ObservationFeatureExtractor.LatestValue(observations, GlucoseCodes);
            float fastingBloodSugar;
            if (glucose.HasValue)
            {
                fastingBloodSugar = glucose.Value > 120 ? 1f : 0f;
            }
            else
            {
                fastingBloodSugar = MedianFastingBloodSugar;
                imputed.Add("fbs");
            }

            var inputTensor = new DenseTensor<float>(
                new[] { age, sex, restingBp, cholesterol, maxHeartRate, fastingBloodSugar },
                new[] { 1, 6 });
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

            return new CardiovascularRiskAssessment(
                patientId,
                age,
                sex,
                restingBp,
                cholesterol,
                maxHeartRate,
                fastingBloodSugar,
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

        private static float? ToSexFeature(AdministrativeGender? gender)
        {
            return gender switch
            {
                AdministrativeGender.Male => 1f,
                AdministrativeGender.Female => 0f,
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

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
