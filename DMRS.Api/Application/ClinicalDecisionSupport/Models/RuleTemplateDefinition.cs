namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record RuleTemplateDefinition(
        string TemplateId,
        string Title,
        string Description,
        IReadOnlyList<RuleTemplateParameterDefinition> Parameters);

    public sealed record RuleTemplateParameterDefinition(
        string Name,
        string Type,
        bool Required,
        string Description);
}
