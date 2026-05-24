namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record RuleValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors);
}
