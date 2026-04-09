using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IKnowledgeCache
    {
        Task<DrugKnowledgeEntry?> GetAsync(string queryKey, string knowledgeType, CancellationToken cancellationToken);
        Task<DrugKnowledgeEntry> GetOrAddAsync(
            string queryKey,
            string knowledgeType,
            string source,
            Func<CancellationToken, Task<string>> payloadFactory,
            CancellationToken cancellationToken);
        Task SaveAsync(DrugKnowledgeEntry entry, CancellationToken cancellationToken);
    }
}
