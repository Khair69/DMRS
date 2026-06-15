using System.Text.Json;

namespace DMRS.Client.Features.ExternalAi.Models;

/// <summary>How DMRS authenticates to an external model endpoint. Mirrors the API enum (numeric).</summary>
public enum ExternalAiAuthType
{
    None = 0,
    ApiKey = 1,
    Bearer = 2
}

/// <summary>Client view of a registered external AI model (never includes the secret).</summary>
public sealed class ExternalAiModelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EndpointUrl { get; set; } = string.Empty;
    public ExternalAiAuthType AuthType { get; set; }
    public string? AuthHeaderName { get; set; }
    public bool HasSecret { get; set; }
    public int TimeoutSeconds { get; set; }
    public string? DecisionJsonPath { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Create/update payload. A null/blank <see cref="Secret"/> on update keeps the stored one.</summary>
public sealed class ExternalAiModelInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EndpointUrl { get; set; } = string.Empty;
    public ExternalAiAuthType AuthType { get; set; } = ExternalAiAuthType.None;
    public string? AuthHeaderName { get; set; }
    public string? Secret { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string? DecisionJsonPath { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Result of running a model against one patient.</summary>
public sealed class ExternalAiInferenceResult
{
    public Guid ModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int? StatusCode { get; set; }
    public JsonElement? Decision { get; set; }
    public string? RawResponse { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}
