using DMRS.Api.Domain.ClinicalDecisionSupport;
using System.Text.Json;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record RulePreviewRequest(
        string Hook,
        CdsRuleDefinition Rule,
        JsonElement Context,
        JsonElement? Prefetch);
}
