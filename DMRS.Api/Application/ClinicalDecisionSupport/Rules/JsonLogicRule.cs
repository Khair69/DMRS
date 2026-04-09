using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Rules
{
    public sealed class JsonLogicRule : ICdsRule
    {
        private readonly CdsRuleDefinition _definition;
        private readonly IRuleExpressionEvaluator _expressionEvaluator;
        private readonly ICardTemplateRenderer _templateRenderer;

        public JsonLogicRule(
            CdsRuleDefinition definition,
            IRuleExpressionEvaluator expressionEvaluator,
            ICardTemplateRenderer templateRenderer)
        {
            _definition = definition;
            _expressionEvaluator = expressionEvaluator;
            _templateRenderer = templateRenderer;
        }

        public Task<IReadOnlyList<CdsCard>> EvaluateAsync(CdsContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isMatch = _expressionEvaluator.Evaluate(_definition.ExpressionJson, context);
            if (!isMatch)
            {
                return Task.FromResult<IReadOnlyList<CdsCard>>([]);
            }

            var card = _templateRenderer.Render(_definition.CardTemplateJson, context);
            return Task.FromResult<IReadOnlyList<CdsCard>>([card]);
        }
    }
}
