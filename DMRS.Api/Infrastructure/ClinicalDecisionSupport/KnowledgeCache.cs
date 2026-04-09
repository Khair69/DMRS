using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;
using DMRS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class KnowledgeCache : IKnowledgeCache
    {
        private readonly AppDbContext _dbContext;
        private readonly KnowledgeCacheOptions _options;

        public KnowledgeCache(AppDbContext dbContext, IOptions<KnowledgeCacheOptions> options)
        {
            _dbContext = dbContext;
            _options = options.Value;
        }

        public async Task<DrugKnowledgeEntry?> GetAsync(
            string queryKey,
            string knowledgeType,
            CancellationToken cancellationToken)
        {
            return await _dbContext.DrugKnowledgeEntries
                .FirstOrDefaultAsync(
                    entry => entry.QueryKey == queryKey && entry.KnowledgeType == knowledgeType,
                    cancellationToken);
        }

        public async Task<DrugKnowledgeEntry> GetOrAddAsync(
            string queryKey,
            string knowledgeType,
            string source,
            Func<CancellationToken, Task<string>> payloadFactory,
            CancellationToken cancellationToken)
        {
            var existing = await _dbContext.DrugKnowledgeEntries
                .FirstOrDefaultAsync(
                    entry => entry.QueryKey == queryKey
                        && entry.KnowledgeType == knowledgeType
                        && entry.Source == source,
                    cancellationToken);

            var now = DateTimeOffset.UtcNow;
            var isExpired = existing != null && existing.ExpiresAt <= now;

            if (existing != null && !isExpired)
            {
                return existing;
            }

            var payload = await payloadFactory(cancellationToken);
            var expiresAt = now.AddDays(_options.CacheTtlDays <= 0 ? 30 : _options.CacheTtlDays);

            if (existing == null)
            {
                existing = new DrugKnowledgeEntry
                {
                    Id = Guid.NewGuid(),
                    QueryKey = queryKey,
                    KnowledgeType = knowledgeType,
                    Source = source
                };
                _dbContext.DrugKnowledgeEntries.Add(existing);
            }

            existing.PayloadJson = payload;
            existing.FetchedAt = now;
            existing.ExpiresAt = expiresAt;

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }

        public async Task SaveAsync(DrugKnowledgeEntry entry, CancellationToken cancellationToken)
        {
            _dbContext.DrugKnowledgeEntries.Update(entry);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
