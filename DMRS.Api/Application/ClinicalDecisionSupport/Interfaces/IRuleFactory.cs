using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IRuleFactory
    {
        IReadOnlyList<ICdsRule> CreateRules(IReadOnlyList<CdsRuleDefinition> definitions);
    }
}
