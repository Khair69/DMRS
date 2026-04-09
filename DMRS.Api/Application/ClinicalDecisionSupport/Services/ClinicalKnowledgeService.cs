using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class ClinicalKnowledgeService : IClinicalKnowledgeService
    {
        private const string IngredientsType = "ingredients";
        private const string MaxDoseType = "max-dose";

        private readonly IKnowledgeProvider _provider;
        private readonly IKnowledgeCache _cache;

        public ClinicalKnowledgeService(IKnowledgeProvider provider, IKnowledgeCache cache)
        {
            _provider = provider;
            _cache = cache;
        }

        public async Task<IReadOnlyList<string>> GetMedicationIngredientsAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            var entry = await _cache.GetOrAddAsync(
                medicationCode,
                IngredientsType,
                _provider.SourceName,
                async ct =>
                {
                    var ingredients = await _provider.GetMedicationIngredientsAsync(medicationCode, ct);
                    return JsonSerializer.Serialize(ingredients);
                },
                cancellationToken);

            var list = JsonSerializer.Deserialize<List<string>>(entry.PayloadJson) ?? [];
            return list;
        }

        public async Task<MaxDoseResult?> GetMaxDoseAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            var entry = await _cache.GetOrAddAsync(
                medicationCode,
                MaxDoseType,
                _provider.SourceName,
                async ct =>
                {
                    var maxDose = await _provider.GetMaxDoseAsync(medicationCode, ct);
                    return JsonSerializer.Serialize(maxDose);
                },
                cancellationToken);

            return JsonSerializer.Deserialize<MaxDoseResult?>(entry.PayloadJson);
        }

        public async Task<bool> HasAllergyContraindicationAsync(
            string medicationCode,
            IReadOnlyList<string> allergyCodes,
            CancellationToken cancellationToken)
        {
            if (allergyCodes.Count == 0)
            {
                return false;
            }

            var ingredients = await GetMedicationIngredientsAsync(medicationCode, cancellationToken);
            if (ingredients.Count == 0)
            {
                return false;
            }

            var allergySet = new HashSet<string>(allergyCodes, StringComparer.OrdinalIgnoreCase);
            return ingredients.Any(ingredient => allergySet.Contains(ingredient));
        }
    }
}
