using DMRS.Api.Domain.ExternalAi;

namespace DMRS.Api.Application.ExternalAi.Models
{
    /// <summary>
    /// Minimal, clinician-facing view of an active external AI model — just enough to populate the
    /// "run against this patient" picker. Deliberately omits the endpoint URL, auth details and audit
    /// metadata, which stay admin-only (they reveal where patient data would be sent).
    /// </summary>
    public sealed record ExternalAiModelSummaryDto(
        Guid Id,
        string Name,
        string? Description)
    {
        public static ExternalAiModelSummaryDto FromEntity(ExternalAiModel model) => new(
            model.Id,
            model.Name,
            model.Description);
    }
}
