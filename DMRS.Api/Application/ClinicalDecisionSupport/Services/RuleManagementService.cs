using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;
using Microsoft.AspNetCore.Http;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class RuleManagementService : IRuleManagementService
    {
        private readonly IRuleDefinitionRepository _repository;
        private readonly IRuleDefinitionValidator _validator;
        private readonly ICdsContextBuilder _contextBuilder;
        private readonly IRuleFactory _ruleFactory;
        private readonly IRuleEngine _ruleEngine;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public RuleManagementService(
            IRuleDefinitionRepository repository,
            IRuleDefinitionValidator validator,
            ICdsContextBuilder contextBuilder,
            IRuleFactory ruleFactory,
            IRuleEngine ruleEngine,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _validator = validator;
            _contextBuilder = contextBuilder;
            _ruleFactory = ruleFactory;
            _ruleEngine = ruleEngine;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<CdsRuleDefinition> CreateAsync(CdsRuleDefinition rule, CancellationToken cancellationToken)
        {
            EnsureValid(rule);
            var actor = GetActor();
            var now = DateTimeOffset.UtcNow;

            rule.Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id;
            rule.Status = CdsRuleStatus.Draft;
            rule.HasUnpublishedChanges = true;
            rule.IsActive = false;
            rule.PublishedVersionId = null;
            rule.PublishedVersionNumber = null;
            rule.PublishedAt = null;
            rule.PublishedBy = null;
            rule.CreatedBy = actor;
            rule.UpdatedBy = actor;
            rule.CreatedAt = now;
            rule.UpdatedAt = now;
            await _repository.AddAsync(rule, cancellationToken);
            return rule;
        }

        public async Task<CdsRuleDefinition?> CloneAsync(Guid id, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return null;
            }

            var clone = new CdsRuleDefinition
            {
                HookId = existing.HookId,
                Name = $"{existing.Name} (Copy)",
                Description = existing.Description,
                Priority = existing.Priority,
                ExpressionJson = existing.ExpressionJson,
                CardTemplateJson = existing.CardTemplateJson
            };

            return await CreateAsync(clone, cancellationToken);
        }

        public Task<CdsRuleDefinition?> GetAsync(Guid id, CancellationToken cancellationToken)
            => _repository.GetByIdAsync(id, cancellationToken);

        public Task<IReadOnlyList<CdsRuleDefinition>> ListAsync(CancellationToken cancellationToken)
            => _repository.ListAsync(cancellationToken);

        public Task<IReadOnlyList<CdsRuleVersion>> ListVersionsAsync(Guid id, CancellationToken cancellationToken)
            => _repository.ListVersionsAsync(id, cancellationToken);

        public async Task<CdsRuleDefinition?> UpdateAsync(Guid id, CdsRuleDefinition update, CancellationToken cancellationToken)
        {
            EnsureValid(update);
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return null;
            }

            if (existing.Status == CdsRuleStatus.Archived)
            {
                throw new ArgumentException("Archived rules cannot be edited.");
            }

            existing.Name = update.Name;
            existing.Description = update.Description;
            existing.HookId = update.HookId;
            existing.Priority = update.Priority;
            existing.ExpressionJson = update.ExpressionJson;
            existing.CardTemplateJson = update.CardTemplateJson;
            existing.HasUnpublishedChanges = true;
            existing.UpdatedBy = GetActor();
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

            if (existing.Status != CdsRuleStatus.Published || existing.PublishedVersionId == null)
            {
                return false;
            }

            existing.IsActive = isActive;
            existing.UpdatedBy = GetActor();
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(existing, cancellationToken);
            return true;
        }

        public async Task<CdsRuleDefinition?> PublishAsync(Guid id, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return null;
            }

            if (existing.Status == CdsRuleStatus.Archived)
            {
                throw new ArgumentException("Archived rules cannot be published.");
            }

            EnsureValid(existing);

            var actor = GetActor();
            var now = DateTimeOffset.UtcNow;
            var versions = await _repository.ListVersionsAsync(id, cancellationToken);
            var nextVersionNumber = versions.Count == 0 ? 1 : versions.Max(v => v.VersionNumber) + 1;

            var version = new CdsRuleVersion
            {
                Id = Guid.NewGuid(),
                RuleDefinitionId = existing.Id,
                VersionNumber = nextVersionNumber,
                HookId = existing.HookId,
                Name = existing.Name,
                Description = existing.Description,
                Priority = existing.Priority,
                ExpressionJson = existing.ExpressionJson,
                CardTemplateJson = existing.CardTemplateJson,
                IsActive = true,
                PublishedAt = now,
                PublishedBy = actor
            };

            existing.Status = CdsRuleStatus.Published;
            existing.IsActive = true;
            existing.HasUnpublishedChanges = false;
            existing.PublishedVersionId = version.Id;
            existing.PublishedVersionNumber = version.VersionNumber;
            existing.PublishedAt = version.PublishedAt;
            existing.PublishedBy = actor;
            existing.UpdatedBy = actor;
            existing.UpdatedAt = now;

            await _repository.PublishAsync(existing, version, cancellationToken);
            return existing;
        }

        public async Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken)
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing == null)
            {
                return false;
            }

            existing.Status = CdsRuleStatus.Archived;
            existing.IsActive = false;
            existing.UpdatedBy = GetActor();
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

        private string GetActor()
        {
            var actor = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            return string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim();
        }
    }
}
