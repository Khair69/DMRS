using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class RuleManagementService : IRuleManagementService
    {
        private readonly IRuleDefinitionRepository _repository;
        private readonly IRuleDefinitionValidator _validator;
        private readonly ICdsContextBuilder _contextBuilder;
        private readonly IRuleFactory _ruleFactory;
        private readonly IRuleEngine _ruleEngine;

        public RuleManagementService(
            IRuleDefinitionRepository repository,
            IRuleDefinitionValidator validator,
            ICdsContextBuilder contextBuilder,
            IRuleFactory ruleFactory,
            IRuleEngine ruleEngine)
        {
            _repository = repository;
            _validator = validator;
            _contextBuilder = contextBuilder;
            _ruleFactory = ruleFactory;
            _ruleEngine = ruleEngine;
        }

        public async Task<CdsRuleDefinition> CreateAsync(CdsRuleDefinition rule, CancellationToken cancellationToken)
        {
            EnsureValid(rule);
            rule.Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id;
            rule.CreatedAt = DateTimeOffset.UtcNow;
            rule.UpdatedAt = rule.CreatedAt;
            await _repository.AddAsync(rule, cancellationToken);
            return rule;
        }

        public Task<CdsRuleDefinition?> GetAsync(Guid id, CancellationToken cancellationToken)
            => _repository.GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<CdsRuleDefinition>> ListAsync(CancellationToken cancellationToken)
            => _repository.ListAsync(cancellationToken);

        public async Task<CdsRuleDefinition?> UpdateAsync(Guid id, CdsRuleDefinition update, CancellationToken cancellationToken)
        {
            EnsureValid(update);
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return null;
            }

            existing.Name = update.Name;
            existing.Description = update.Description;
            existing.HookId = update.HookId;
            existing.Priority = update.Priority;
            existing.IsActive = update.IsActive;
            existing.ExpressionJson = update.ExpressionJson;
            existing.CardTemplateJson = update.CardTemplateJson;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            await _repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        public async Task<bool> ActivateAsync(Guid id, bool isActive, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return false;
            }

            existing.IsActive = isActive;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(existing, cancellationToken);
            return true;
        }

        public RuleValidationResult Validate(CdsRuleDefinition rule) => _validator.Validate(rule);

        public async Task<RulePreviewResponse> PreviewAsync(RulePreviewRequest request, CancellationToken cancellationToken)
        {
            var validation = Validate(request.Rule);
            if (!validation.IsValid)
            {
                return new RulePreviewResponse(validation, []);
            }

            var hookRequest = new CdsHookRequest(
                request.Hook,
                Guid.NewGuid().ToString("N"),
                request.Context,
                request.Prefetch);

            var context = await _contextBuilder.BuildAsync(hookRequest, cancellationToken);
            var rules = _ruleFactory.CreateRules([request.Rule]);
            var cards = await _ruleEngine.EvaluateAsync(context, rules, cancellationToken);
            return new RulePreviewResponse(validation, cards);
        }

        private void EnsureValid(CdsRuleDefinition rule)
        {
            var validation = Validate(rule);
            if (!validation.IsValid)
            {
                throw new ArgumentException(string.Join(" ", validation.Errors));
            }
        }
    }
}
