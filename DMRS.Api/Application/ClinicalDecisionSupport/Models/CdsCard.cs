namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record CdsCard(
        string Summary,
        string? Detail,
        string Indicator,
        CdsCardSource Source);

    public sealed record CdsCardSource(
        string Label,
        string? Url);
}
