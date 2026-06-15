using DMRS.Api.Domain.ExternalAi;

namespace DMRS.Api.Application.ExternalAi.Models
{
    /// <summary>
    /// Client-facing view of a registered external AI model. Deliberately omits the secret; exposes
    /// only whether one is stored so the admin UI can show "configured" without ever reading it back.
    /// </summary>
    public sealed record ExternalAiModelDto(
        Guid Id,
        string Name,
        string? Description,
        string EndpointUrl,
        ExternalAiAuthType AuthType,
        string? AuthHeaderName,
        bool HasSecret,
        int TimeoutSeconds,
        string? DecisionJsonPath,
        bool IsActive,
        string? CreatedBy,
        string? UpdatedBy,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static ExternalAiModelDto FromEntity(ExternalAiModel model) => new(
            model.Id,
            model.Name,
            model.Description,
            model.EndpointUrl,
            model.AuthType,
            model.AuthHeaderName,
            !string.IsNullOrEmpty(model.EncryptedSecret),
            model.TimeoutSeconds,
            model.DecisionJsonPath,
            model.IsActive,
            model.CreatedBy,
            model.UpdatedBy,
            model.CreatedAt,
            model.UpdatedAt);
    }
}
