namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record CdsServiceDefinition(
        string Id,
        string Hook,
        string Title,
        string Description);
}
