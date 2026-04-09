using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IRuleDefinitionRepository
    {
        Task<IReadOnlyList<CdsRuleDefinition>> GetActiveByHookAsync(string hookId, CancellationToken cancellationToken);
        Task<IReadOnlyList<CdsRuleDefinition>> ListAsync(CancellationToken cancellationToken);
        Task<CdsRuleDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
        Task AddAsync(CdsRuleDefinition rule, CancellationToken cancellationToken);
        Task UpdateAsync(CdsRuleDefinition rule, CancellationToken cancellationToken);
    }
}
