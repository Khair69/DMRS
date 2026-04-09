using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class RuleManagementService : IRuleManagementService
    {
        private readonly IRuleDefinitionRepository _repository;

        public RuleManagementService(IRuleDefinitionRepository repository)
        {
            _repository = repository;
        }

        public async Task<CdsRuleDefinition> CreateAsync(CdsRuleDefinition rule, CancellationToken cancellationToken)
        {
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
    }
}
