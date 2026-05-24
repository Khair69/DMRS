using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IRuleManagementService
    {
        Task<CdsRuleDefinition> CreateAsync(CdsRuleDefinition rule, CancellationToken cancellationToken);
        Task<CdsRuleDefinition?> CloneAsync(Guid id, CancellationToken cancellationToken);
        Task<CdsRuleDefinition?> GetAsync(Guid id, CancellationToken cancellationToken);
        Task<IReadOnlyList<CdsRuleDefinition>> ListAsync(CancellationToken cancellationToken);
        Task<IReadOnlyList<CdsRuleVersion>> ListVersionsAsync(Guid id, CancellationToken cancellationToken);
        Task<CdsRuleDefinition?> UpdateAsync(Guid id, CdsRuleDefinition update, CancellationToken cancellationToken);
        Task<bool> ActivateAsync(Guid id, bool isActive, CancellationToken cancellationToken);
        Task<CdsRuleDefinition?> PublishAsync(Guid id, CancellationToken cancellationToken);
        Task<bool> ArchiveAsync(Guid id, CancellationToken cancellationToken);
        RuleValidationResult Validate(CdsRuleDefinition rule);
        Task<RulePreviewResponse> PreviewAsync(RulePreviewRequest request, CancellationToken cancellationToken);
    }
}
