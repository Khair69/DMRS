using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using Microsoft.Extensions.Options;

namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class InMemoryDrugKnowledgeService : IDrugKnowledgeService
    {
        private readonly IReadOnlyList<DrugKnowledge> _entries;

        public InMemoryDrugKnowledgeService(IOptions<DrugKnowledgeOptions> options)
        {
            _entries = options.Value.Entries
                .Where(e => !string.IsNullOrWhiteSpace(e.Code) && e.MaxDailyDoseMg > 0)
                .Select(e => new DrugKnowledge(e.Code.Trim().ToLowerInvariant(), e.MaxDailyDoseMg, e.Display?.Trim()))
                .ToList();
        }

        public Task<IReadOnlyList<DrugKnowledge>> FindByCodesAsync(IEnumerable<string> medicationCodes, CancellationToken cancellationToken = default)
        {
            var codeSet = new HashSet<string>(medicationCodes, StringComparer.OrdinalIgnoreCase);
            var matches = _entries
                .Where(entry => codeSet.Contains(entry.Code))
                .ToList();

            return Task.FromResult<IReadOnlyList<DrugKnowledge>>(matches);
        }
    }
}

