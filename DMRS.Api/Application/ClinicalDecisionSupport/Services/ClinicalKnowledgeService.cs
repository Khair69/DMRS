using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class ClinicalKnowledgeService : IClinicalKnowledgeService
    {
        private readonly IMedicineKnowledgeService _medicineKnowledgeService;

        public ClinicalKnowledgeService(IMedicineKnowledgeService medicineKnowledgeService)
        {
            _medicineKnowledgeService = medicineKnowledgeService;
        }

        public Task<MedicineKnowledge?> GetMedicationKnowledgeAsync(
            string medicationCode,
            CancellationToken cancellationToken)
            => _medicineKnowledgeService.GetAsync(medicationCode, cancellationToken);

        public async Task<IReadOnlyList<string>> GetMedicationIngredientsAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            var knowledge = await _medicineKnowledgeService.GetAsync(medicationCode, cancellationToken);
            if (knowledge == null)
            {
                return [];
            }

            return knowledge.Ingredients
                .Select(ingredient => ingredient.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public async Task<MaxDoseResult?> GetMaxDoseAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            var knowledge = await _medicineKnowledgeService.GetAsync(medicationCode, cancellationToken);
            if (knowledge?.MaxDailyMg == null)
            {
                return null;
            }

            return new MaxDoseResult(
                knowledge.MaxDailyMg.Value,
                "mg",
                "daily",
                $"{knowledge.Source}:{knowledge.RxCui}");
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
