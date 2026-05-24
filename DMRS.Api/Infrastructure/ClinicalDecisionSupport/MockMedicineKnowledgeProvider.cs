using System.Net.Http.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using Microsoft.Extensions.Options;

namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class MockMedicineKnowledgeProvider : IKnowledgeProvider
    {
        private readonly HttpClient _httpClient;

        public MockMedicineKnowledgeProvider(
            HttpClient httpClient,
            IOptions<MockMedicineApiOptions> options)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
        }

        public string SourceName => "MockMedicineApi";

        public async Task<MedicineKnowledge?> GetMedicationKnowledgeAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            var medicine = await GetMedicineAsync(medicationCode, cancellationToken);
            if (medicine == null)
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var ingredients = (medicine.Ingredients ?? [])
                .Where(ingredient => !string.IsNullOrWhiteSpace(ingredient.Code) || !string.IsNullOrWhiteSpace(ingredient.Name))
                .Select(ingredient => new MedicineIngredient(
                    ingredient.Code?.Trim() ?? string.Empty,
                    ingredient.Name?.Trim() ?? string.Empty))
                .ToArray();

            return new MedicineKnowledge(
                medicine.RxCui,
                medicine.Name,
                medicine.Dosing?.MaxDailyMg,
                medicine.Dosing?.MaxSingleMg,
                medicine.Dosing?.WarningThreshold,
                medicine.Safety?.PregnancyCategory,
                medicine.Safety?.IsControlled,
                ingredients,
                medicine.Indications?.Where(code => !string.IsNullOrWhiteSpace(code)).ToArray() ?? [],
                SourceName,
                now,
                now.AddDays(30));
        }

        public async Task<IReadOnlyList<string>> GetMedicationIngredientsAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            var medicine = await GetMedicineAsync(medicationCode, cancellationToken);
            if (medicine?.Ingredients == null || medicine.Ingredients.Count == 0)
            {
                return [];
            }

            return medicine.Ingredients
                .Select(ingredient => ingredient.Code)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public async Task<MaxDoseResult?> GetMaxDoseAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            var medicine = await GetMedicineAsync(medicationCode, cancellationToken);
            if (medicine?.Dosing == null)
            {
                return null;
            }

            return new MaxDoseResult(
                medicine.Dosing.MaxDailyMg,
                "mg",
                "daily",
                $"{SourceName}:{medicine.RxCui}");
        }

        private async Task<MockMedicineDto?> GetMedicineAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(medicationCode))
            {
                return null;
            }

            var response = await _httpClient.GetAsync(
                $"api/medications/{Uri.EscapeDataString(medicationCode)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<MockMedicineDto>(
                cancellationToken: cancellationToken);
        }

        private sealed class MockMedicineDto
        {
            public string RxCui { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public MockDosingDto Dosing { get; set; } = new();
            public MockSafetyDto Safety { get; set; } = new();
            public List<string> Indications { get; set; } = [];
            public List<MockIngredientDto> Ingredients { get; set; } = [];
        }

        private sealed class MockDosingDto
        {
            public decimal MaxDailyMg { get; set; }
            public decimal? MaxSingleMg { get; set; }
            public decimal? WarningThreshold { get; set; }
        }

        private sealed class MockSafetyDto
        {
            public string? PregnancyCategory { get; set; }
            public bool? IsControlled { get; set; }
        }

        private sealed class MockIngredientDto
        {
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
        }
    }
}
