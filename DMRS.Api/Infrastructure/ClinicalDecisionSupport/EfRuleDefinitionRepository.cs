using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Domain.ClinicalDecisionSupport;
using DMRS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class EfRuleDefinitionRepository : IRuleDefinitionRepository
    {
        private readonly AppDbContext _dbContext;

        public EfRuleDefinitionRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<CdsRuleDefinition>> GetActiveByHookAsync(
            string hookId,
            CancellationToken cancellationToken)
        {
            return await (
                from rule in _dbContext.CdsRuleDefinitions.AsNoTracking()
                join version in _dbContext.CdsRuleVersions.AsNoTracking()
                    on rule.PublishedVersionId equals version.Id
                where version.HookId == hookId
                    && rule.Status == CdsRuleStatus.Published
                    && rule.IsActive
                orderby version.Priority
                select new CdsRuleDefinition
                {
                    Id = rule.Id,
                    HookId = version.HookId,
                    Name = version.Name,
                    Description = version.Description,
                    Priority = version.Priority,
                    IsActive = rule.IsActive,
                    Status = rule.Status,
                    HasUnpublishedChanges = rule.HasUnpublishedChanges,
                    PublishedVersionId = rule.PublishedVersionId,
                    PublishedVersionNumber = rule.PublishedVersionNumber,
                    PublishedAt = rule.PublishedAt,
                    CreatedBy = rule.CreatedBy,
                    UpdatedBy = rule.UpdatedBy,
                    PublishedBy = rule.PublishedBy,
                    ExpressionJson = version.ExpressionJson,
                    CardTemplateJson = version.CardTemplateJson,
                    CreatedAt = rule.CreatedAt,
                    UpdatedAt = rule.UpdatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CdsRuleDefinition>> ListAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.CdsRuleDefinitions
                .OrderBy(r => r.HookId)
                .ThenBy(r => r.Priority)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CdsRuleVersion>> ListVersionsAsync(Guid ruleId, CancellationToken cancellationToken)
        {
            return await _dbContext.CdsRuleVersions
                .AsNoTracking()
                .Where(v => v.RuleDefinitionId == ruleId)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync(cancellationToken);
        }

        public Task<CdsRuleDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return _dbContext.CdsRuleDefinitions
                .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        }

        public async Task AddAsync(CdsRuleDefinition rule, CancellationToken cancellationToken)
        {
            _dbContext.CdsRuleDefinitions.Add(rule);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task UpdateAsync(CdsRuleDefinition rule, CancellationToken cancellationToken)
        {
            _dbContext.CdsRuleDefinitions.Update(rule);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task PublishAsync(CdsRuleDefinition rule, CdsRuleVersion version, CancellationToken cancellationToken)
        {
            _dbContext.CdsRuleVersions.Add(version);
            _dbContext.CdsRuleDefinitions.Update(rule);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
