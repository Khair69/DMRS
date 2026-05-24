using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Rules;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class RuleFactory : IRuleFactory
    {
        private readonly IRuleExpressionEvaluator _expressionEvaluator;
        private readonly ICardTemplateRenderer _templateRenderer;

        public RuleFactory(
            IRuleExpressionEvaluator expressionEvaluator,
            ICardTemplateRenderer templateRenderer)
        {
            _expressionEvaluator = expressionEvaluator;
            _templateRenderer = templateRenderer;
        }

        public IReadOnlyList<ICdsRule> CreateRules(IReadOnlyList<CdsRuleDefinition> definitions)
        {
            return definitions
                .OrderBy(d => d.Priority)
                .Select(d => (ICdsRule)new JsonLogicRule(d, _expressionEvaluator, _templateRenderer))
                .ToList();
        }
    }
}
