using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IRuleDefinitionValidator
    {
        RuleValidationResult Validate(CdsRuleDefinition rule);
    }
}
