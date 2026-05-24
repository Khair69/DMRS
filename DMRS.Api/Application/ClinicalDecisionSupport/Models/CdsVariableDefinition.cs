namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record CdsVariableDefinition(
        string Path,
        string Type,
        string Description);
}
