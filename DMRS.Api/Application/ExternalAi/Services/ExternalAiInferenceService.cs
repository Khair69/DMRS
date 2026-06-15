using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DMRS.Api.Application.ExternalAi.Interfaces;
using DMRS.Api.Application.ExternalAi.Models;
using DMRS.Api.Domain.ExternalAi;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Application.ExternalAi.Services
{
    /// <summary>
    /// Collects a patient's FHIR record into a Bundle and POSTs it to a registered external model
    /// endpoint, returning the model's JSON decision. Transport failures are captured in the result
    /// rather than thrown so callers always get a structured outcome.
    /// </summary>
    public sealed class ExternalAiInferenceService : IExternalAiInferenceService
    {
        private const string FhirContentType = "application/fhir+json";

        // Resource types gathered into the outbound bundle (alongside the Patient itself).
        private static readonly string[] PatientCompartmentTypes =
        {
            nameof(Observation),
            nameof(Condition),
            nameof(MedicationRequest),
            nameof(AllergyIntolerance)
        };

        private readonly HttpClient _httpClient;
        private readonly IFhirRepository _fhirRepository;
        private readonly IExternalAiModelRepository _modelRepository;
        private readonly IExternalAiSecretProtector _secretProtector;
        private readonly FhirJsonSerializer _serializer;
        private readonly ILogger<ExternalAiInferenceService> _logger;

        public ExternalAiInferenceService(
            HttpClient httpClient,
            IFhirRepository fhirRepository,
            IExternalAiModelRepository modelRepository,
            IExternalAiSecretProtector secretProtector,
            FhirJsonSerializer serializer,
            ILogger<ExternalAiInferenceService> logger)
        {
            _httpClient = httpClient;
            _fhirRepository = fhirRepository;
            _modelRepository = modelRepository;
            _secretProtector = secretProtector;
            _serializer = serializer;
            _logger = logger;
        }

        public async Task<ExternalAiInferenceResult?> RunAsync(Guid modelId, string patientId, CancellationToken cancellationToken)
        {
            var model = await _modelRepository.GetByIdAsync(modelId, cancellationToken);
            if (model is null || !model.IsActive)
            {
                return null;
            }

            var normalizedPatientId = NormalizePatientId(patientId);
            var bundle = await BuildPatientBundleAsync(normalizedPatientId, cancellationToken);
            if (bundle is null)
            {
                return Failure(model, normalizedPatientId, statusCode: null, error: "Patient not found.", durationMs: 0);
            }

            var payload = _serializer.SerializeToString(bundle);
            var stopwatch = Stopwatch.StartNew();

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(model.TimeoutSeconds));

                using var request = new HttpRequestMessage(HttpMethod.Post, model.EndpointUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, FhirContentType)
                };
                ApplyAuth(request, model);

                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                stopwatch.Stop();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "External AI model '{ModelName}' ({ModelId}) returned {StatusCode} for patient {PatientId}.",
                        model.Name, model.Id, (int)response.StatusCode, normalizedPatientId);

                    return Failure(model, normalizedPatientId, (int)response.StatusCode,
                        $"Model returned HTTP {(int)response.StatusCode}.", stopwatch.ElapsedMilliseconds, body);
                }

                var decision = ExtractDecision(body, model.DecisionJsonPath);

                return new ExternalAiInferenceResult(
                    model.Id,
                    model.Name,
                    normalizedPatientId,
                    Success: true,
                    StatusCode: (int)response.StatusCode,
                    Decision: decision,
                    RawResponse: body,
                    Error: null,
                    DurationMs: stopwatch.ElapsedMilliseconds,
                    EvaluatedAt: DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                return Failure(model, normalizedPatientId, statusCode: null,
                    error: $"Model did not respond within {model.TimeoutSeconds}s.", durationMs: stopwatch.ElapsedMilliseconds);
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "External AI model '{ModelName}' ({ModelId}) request failed.", model.Name, model.Id);
                return Failure(model, normalizedPatientId, statusCode: null,
                    error: $"Could not reach the model endpoint: {ex.Message}", durationMs: stopwatch.ElapsedMilliseconds);
            }
        }

        private async Task<Bundle?> BuildPatientBundleAsync(string patientId, CancellationToken cancellationToken)
        {
            var patient = await _fhirRepository.GetAsync<Patient>(patientId);
            if (patient is null)
            {
                return null;
            }

            var bundle = new Bundle
            {
                Type = Bundle.BundleType.Collection,
                Timestamp = DateTimeOffset.UtcNow
            };
            bundle.Entry.Add(new Bundle.EntryComponent { Resource = patient });

            var patientReference = $"Patient/{patientId}";
            var query = new Dictionary<string, string> { ["patient"] = patientReference };

            foreach (var resource in await SearchCompartmentAsync(query))
            {
                cancellationToken.ThrowIfCancellationRequested();
                bundle.Entry.Add(new Bundle.EntryComponent { Resource = resource });
            }

            return bundle;
        }

        private async Task<IEnumerable<Resource>> SearchCompartmentAsync(Dictionary<string, string> query)
        {
            var resources = new List<Resource>();
            resources.AddRange(await _fhirRepository.SearchAsync<Observation>(query));
            resources.AddRange(await _fhirRepository.SearchAsync<Condition>(query));
            resources.AddRange(await _fhirRepository.SearchAsync<MedicationRequest>(query));
            resources.AddRange(await _fhirRepository.SearchAsync<AllergyIntolerance>(query));
            return resources;
        }

        private void ApplyAuth(HttpRequestMessage request, ExternalAiModel model)
        {
            if (model.AuthType == ExternalAiAuthType.None || string.IsNullOrEmpty(model.EncryptedSecret))
            {
                return;
            }

            var secret = _secretProtector.Unprotect(model.EncryptedSecret);
            if (string.IsNullOrEmpty(secret))
            {
                _logger.LogWarning("Stored secret for external AI model '{ModelName}' ({ModelId}) could not be decrypted.", model.Name, model.Id);
                return;
            }

            switch (model.AuthType)
            {
                case ExternalAiAuthType.Bearer:
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                    break;
                case ExternalAiAuthType.ApiKey:
                    var header = string.IsNullOrWhiteSpace(model.AuthHeaderName) ? "X-API-Key" : model.AuthHeaderName;
                    request.Headers.TryAddWithoutValidation(header, secret);
                    break;
            }
        }

        // Pulls the configured dot-path out of the response (e.g. "result.label"); returns the whole
        // parsed body when no path is set. Falls back to the raw body wrapped as a JSON string if the
        // response is not valid JSON, so a plain-text decision still surfaces.
        private static JsonElement? ExtractDecision(string body, string? decisionJsonPath)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                using var fallback = JsonDocument.Parse(JsonSerializer.Serialize(body));
                return fallback.RootElement.Clone();
            }

            using (document)
            {
                var element = document.RootElement;
                if (string.IsNullOrWhiteSpace(decisionJsonPath))
                {
                    return element.Clone();
                }

                foreach (var segment in decisionJsonPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(segment, out var next))
                    {
                        // Path didn't resolve — return the whole body so nothing is silently dropped.
                        return document.RootElement.Clone();
                    }

                    element = next;
                }

                return element.Clone();
            }
        }

        private static ExternalAiInferenceResult Failure(
            ExternalAiModel model, string patientId, int? statusCode, string error, long durationMs, string? rawResponse = null)
            => new(
                model.Id,
                model.Name,
                patientId,
                Success: false,
                StatusCode: statusCode,
                Decision: null,
                RawResponse: rawResponse,
                Error: error,
                DurationMs: durationMs,
                EvaluatedAt: DateTimeOffset.UtcNow);

        private static string NormalizePatientId(string patientId)
            => patientId.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase)
                ? patientId["Patient/".Length..]
                : patientId;
    }
}
