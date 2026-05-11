namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record RulePreviewResponse(
        RuleValidationResult Validation,
        IReadOnlyList<CdsCard> Cards);
}
