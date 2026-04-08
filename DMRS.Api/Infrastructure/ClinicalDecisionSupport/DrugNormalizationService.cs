using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain;
using DMRS.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class DrugNormalizationService : IDrugNormalizationService
    {
        private readonly AppDbContext _db;
        private readonly IRxNormClient _rxNormClient;

        public DrugNormalizationService(AppDbContext db, IRxNormClient rxNormClient)
        {
            _db = db;
            _rxNormClient = rxNormClient;
        }

        public async Task<IReadOnlyList<string>> NormalizeAsync(DrugConcept concept, CancellationToken cancellationToken = default)
        {
            var sourceTerm = ResolveSourceTerm(concept);
            var sourceSystem = ResolveSourceSystem(concept);

            if (string.IsNullOrWhiteSpace(sourceTerm))
            {
                return [];
            }

            var cached = await _db.DrugMappings
                .Where(x => x.SourceTerm == sourceTerm && x.SourceSystem == sourceSystem)
                .Select(x => x.IngredientRxCui)
                .ToListAsync(cancellationToken);

            if (cached.Count > 0)
            {
                return cached;
            }

            var rxcui = await ResolveRxCuiAsync(sourceTerm, sourceSystem, cancellationToken);
            if (string.IsNullOrWhiteSpace(rxcui))
            {
                return [];
            }

            var ingredientIds = await _rxNormClient.GetIngredientRxCuisAsync(rxcui, cancellationToken);
            if (ingredientIds.Count == 0)
            {
                ingredientIds = [rxcui];
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var ingredient in ingredientIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                _db.DrugMappings.Add(new DrugMapping
                {
                    Id = Guid.NewGuid(),
                    SourceTerm = sourceTerm,
                    SourceSystem = sourceSystem,
                    IngredientRxCui = ingredient,
                    LastUpdatedUtc = now
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
            return ingredientIds;
        }

        private async Task<string?> ResolveRxCuiAsync(string sourceTerm, string sourceSystem, CancellationToken cancellationToken)
        {
            if (sourceSystem.Contains("rxnorm", StringComparison.OrdinalIgnoreCase))
            {
                return sourceTerm;
            }

            if (sourceSystem.Contains("ndc", StringComparison.OrdinalIgnoreCase))
            {
                return await _rxNormClient.GetRxCuiByNdcAsync(sourceTerm, cancellationToken);
            }

            return await _rxNormClient.GetRxCuiByNameAsync(sourceTerm, cancellationToken);
        }

        private static string ResolveSourceTerm(DrugConcept concept)
        {
            if (!string.IsNullOrWhiteSpace(concept.Code))
            {
                return concept.Code.Trim().ToLowerInvariant();
            }

            return concept.Display?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static string ResolveSourceSystem(DrugConcept concept)
        {
            if (!string.IsNullOrWhiteSpace(concept.System))
            {
                return concept.System.Trim().ToLowerInvariant();
            }

            return "text";
        }
    }
}
