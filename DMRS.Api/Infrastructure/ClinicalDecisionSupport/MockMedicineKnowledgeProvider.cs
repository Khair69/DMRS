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
            public MockDosingDto Dosing { get; set; } = new();
            public List<MockIngredientDto> Ingredients { get; set; } = [];
        }

        private sealed class MockDosingDto
        {
            public decimal MaxDailyMg { get; set; }
        }

        private sealed class MockIngredientDto
        {
            public string Code { get; set; } = string.Empty;
        }
    }
}
