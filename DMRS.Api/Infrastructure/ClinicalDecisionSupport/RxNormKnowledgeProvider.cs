using System.Net.Http.Json;
using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using Microsoft.Extensions.Options;

namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class RxNormKnowledgeProvider : IKnowledgeProvider
    {
        private readonly HttpClient _httpClient;

        public RxNormKnowledgeProvider(HttpClient httpClient, IOptions<RxNormOptions> options)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
        }

        public string SourceName => "RxNorm";

        public async Task<IReadOnlyList<string>> GetMedicationIngredientsAsync(
            string medicationCode,
            CancellationToken cancellationToken)
        {
            var rxcui = await ResolveRxCuiAsync(medicationCode, cancellationToken);
            if (string.IsNullOrWhiteSpace(rxcui))
            {
                return [];
            }

            var url = $"rxcui/{Uri.EscapeDataString(rxcui)}/related.json?tty=IN+PIN";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (json.ValueKind == JsonValueKind.Undefined)
            {
                return [];
            }

            return ExtractIngredientIds(json);
        }

        public Task<MaxDoseResult?> GetMaxDoseAsync(string medicationCode, CancellationToken cancellationToken)
        {
            return Task.FromResult<MaxDoseResult?>(null);
        }

        private async Task<string?> ResolveRxCuiAsync(string medicationCode, CancellationToken cancellationToken)
        {
            if (medicationCode.All(char.IsDigit))
            {
                return medicationCode;
            }

            var url = $"rxcui.json?name={Uri.EscapeDataString(medicationCode)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (json.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!json.TryGetProperty("idGroup", out var idGroup)
                || idGroup.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!idGroup.TryGetProperty("rxnormId", out var rxIds)
                || rxIds.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var first = rxIds.EnumerateArray().FirstOrDefault();
            return first.ValueKind == JsonValueKind.String ? first.GetString() : null;
        }

        private static IReadOnlyList<string> ExtractIngredientIds(JsonElement json)
        {
            if (!json.TryGetProperty("relatedGroup", out var relatedGroup)
                || relatedGroup.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            if (!relatedGroup.TryGetProperty("conceptGroup", out var conceptGroup)
                || conceptGroup.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var results = new List<string>();

            foreach (var group in conceptGroup.EnumerateArray())
            {
                if (!group.TryGetProperty("conceptProperties", out var properties)
                    || properties.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var concept in properties.EnumerateArray())
                {
                    if (concept.TryGetProperty("rxcui", out var rxcuiElement)
                        && rxcuiElement.ValueKind == JsonValueKind.String)
                    {
                        var rxcui = rxcuiElement.GetString();
                        if (!string.IsNullOrWhiteSpace(rxcui))
                        {
                            results.Add(rxcui);
                        }
                    }
                }
            }

            return results;
        }
    }
}
