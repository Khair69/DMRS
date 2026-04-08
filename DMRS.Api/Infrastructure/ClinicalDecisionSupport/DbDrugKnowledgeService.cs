using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class DbDrugKnowledgeService : IDrugKnowledgeService
    {
        private readonly AppDbContext _db;

        public DbDrugKnowledgeService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IReadOnlyList<DrugKnowledge>> FindByCodesAsync(IEnumerable<string> ingredientRxCuis, CancellationToken cancellationToken = default)
        {
            var ids = ingredientRxCuis
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ids.Count == 0)
            {
                return [];
            }

            return await _db.DrugMaxDoses
                .Where(x => ids.Contains(x.IngredientRxCui))
                .Select(x => new DrugKnowledge(x.IngredientRxCui, x.MaxDailyDoseMg, x.Display))
                .ToListAsync(cancellationToken);
        }
    }
}
