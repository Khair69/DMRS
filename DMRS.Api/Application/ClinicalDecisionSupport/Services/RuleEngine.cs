using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class RuleEngine : IRuleEngine
    {
        public async Task<IReadOnlyList<CdsCard>> EvaluateAsync(
            CdsContext context,
            IEnumerable<ICdsRule> rules,
            CancellationToken cancellationToken)
        {
            var cards = new List<CdsCard>();

            foreach (var rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ruleCards = await rule.EvaluateAsync(context, cancellationToken);
                if (ruleCards.Count > 0)
                {
                    cards.AddRange(ruleCards);
                }
            }

            return cards;
        }
    }
}
