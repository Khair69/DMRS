using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IRuleTemplateService
    {
        IReadOnlyList<RuleTemplateDefinition> ListTemplates();
        CdsRuleDefinition Compile(RuleTemplateRequest request);
    }
}
