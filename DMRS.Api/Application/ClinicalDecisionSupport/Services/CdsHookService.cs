using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class CdsHookService : ICdsHookService
    {
        private readonly IRuleDefinitionRepository _ruleRepository;
        private readonly IRuleFactory _ruleFactory;
        private readonly IRuleEngine _ruleEngine;
        private readonly ICdsContextBuilder _contextBuilder;

        public CdsHookService(
            IRuleDefinitionRepository ruleRepository,
            IRuleFactory ruleFactory,
            IRuleEngine ruleEngine,
            ICdsContextBuilder contextBuilder)
        {
            _ruleRepository = ruleRepository;
            _ruleFactory = ruleFactory;
            _ruleEngine = ruleEngine;
            _contextBuilder = contextBuilder;
        }

        public async Task<CdsHookResponse> EvaluateAsync(
            string hookId,
            CdsHookRequest request,
            CancellationToken cancellationToken)
        {
            var definitions = await _ruleRepository.GetActiveByHookAsync(hookId, cancellationToken);
            var context = await _contextBuilder.BuildAsync(request, cancellationToken);
            var rules = _ruleFactory.CreateRules(definitions);
            var cards = await _ruleEngine.EvaluateAsync(context, rules, cancellationToken);

            return new CdsHookResponse(cards);
        }
    }
}
