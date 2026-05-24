namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record CdsHookResponse(IReadOnlyList<CdsCard> Cards);
}
