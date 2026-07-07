using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using Microsoft.Extensions.Logging;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class CdsHookService : ICdsHookService
    {
        private readonly IRuleDefinitionRepository _ruleRepository;
        private readonly IRuleFactory _ruleFactory;
        private readonly IRuleEngine _ruleEngine;
        private readonly ICdsContextBuilder _contextBuilder;
        private readonly ILogger<CdsHookService> _logger;

        public CdsHookService(
            IRuleDefinitionRepository ruleRepository,
            IRuleFactory ruleFactory,
            IRuleEngine ruleEngine,
            ICdsContextBuilder contextBuilder,
            ILogger<CdsHookService> logger)
        {
            _ruleRepository = ruleRepository;
            _ruleFactory = ruleFactory;
            _ruleEngine = ruleEngine;
            _contextBuilder = contextBuilder;
            _logger = logger;
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

            // Diagnostic trail for "why did/didn't a card fire?" — surfaces the active-rule count and
            // the medication/dose values the rules actually saw. A published max-dose rule that never
            // fires almost always means rxCui/dose here are null (e.g. an uncoded medication).
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                var medication = context.Data.GetValueOrDefault("medication") as IReadOnlyDictionary<string, object?>;
                var dose = context.Data.GetValueOrDefault("dose") as IReadOnlyDictionary<string, object?>;
                _logger.LogDebug(
                    "CDS hook '{HookId}' evaluated {RuleCount} active rule(s) for patient {PatientId}; " +
                    "medication.rxCui={RxCui}, dose.requestedDailyMg={RequestedDailyMg}, dose.maxDailyMg={MaxDailyMg}; " +
                    "produced {CardCount} card(s).",
                    hookId,
                    definitions.Count,
                    context.PatientId ?? "(none)",
                    medication?.GetValueOrDefault("rxCui") ?? "(none)",
                    dose?.GetValueOrDefault("requestedDailyMg") ?? "(none)",
                    dose?.GetValueOrDefault("maxDailyMg") ?? "(none)",
                    cards.Count);
            }

            return new CdsHookResponse(cards);
        }
    }
}
