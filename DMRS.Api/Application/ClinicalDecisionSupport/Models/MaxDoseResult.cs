namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record MaxDoseResult(
        decimal? MaxDoseValue,
        string? Unit,
        string? Frequency,
        string? SourceReference);
}
