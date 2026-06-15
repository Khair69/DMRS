using System.Text.Json;

namespace DMRS.Api.Application.ExternalAi.Models
{
    /// <summary>
    /// Outcome of sending a patient's FHIR bundle to an external model. On success, <see cref="Decision"/>
    /// holds the parsed response (narrowed to <c>DecisionJsonPath</c> when configured) and
    /// <see cref="RawResponse"/> the full body. On failure, <see cref="Error"/> explains why.
    /// </summary>
    public sealed record ExternalAiInferenceResult(
        Guid ModelId,
        string ModelName,
        string PatientId,
        bool Success,
        int? StatusCode,
        JsonElement? Decision,
        string? RawResponse,
        string? Error,
        long DurationMs,
        DateTimeOffset EvaluatedAt);
}
