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
            return await _dbContext.CdsRuleDefinitions
                .Where(r => r.HookId == hookId && r.IsActive)
                .OrderBy(r => r.Priority)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<CdsRuleDefinition>> ListAsync(CancellationToken cancellationToken)
        {
            return await _dbContext.CdsRuleDefinitions
                .OrderBy(r => r.HookId)
                .ThenBy(r => r.Priority)
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
    }
}
