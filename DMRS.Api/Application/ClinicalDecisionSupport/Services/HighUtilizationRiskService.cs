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

            var inputTensor = new DenseTensor<float>(new[] { ageYears.Value, genderValue.Value }, new[] { 1, 2 });
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_options.InputName, inputTensor)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
            var (label, probability) = ParseOutputs(results);
            var isHighRisk = label ?? (probability.HasValue && probability.Value >= _options.HighRiskThreshold);

            return new HighUtilizationRiskAssessment(
                normalizedPatientId,
                ageYears.Value,
                genderValue.Value,
                isHighRisk,
                probability,
                _modelName,
                DateTimeOffset.UtcNow,
                true);
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
