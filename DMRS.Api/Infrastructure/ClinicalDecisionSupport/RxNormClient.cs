using System.Text.Json;

namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class RxNormClient : IRxNormClient
    {
        private readonly HttpClient _httpClient;

        public RxNormClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string?> GetRxCuiByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            var url = $"rxcui.json?name={Uri.EscapeDataString(name)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (doc.RootElement.TryGetProperty("idGroup", out var idGroup) &&
                idGroup.TryGetProperty("rxnormId", out var rxnormId) &&
                rxnormId.ValueKind == JsonValueKind.Array &&
                rxnormId.GetArrayLength() > 0)
            {
                return rxnormId[0].GetString();
            }

            return null;
        }

        public async Task<string?> GetRxCuiByNdcAsync(string ndc, CancellationToken cancellationToken = default)
        {
            var url = $"rxcui.json?idtype=ndc&id={Uri.EscapeDataString(ndc)}";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (doc.RootElement.TryGetProperty("idGroup", out var idGroup) &&
                idGroup.TryGetProperty("rxnormId", out var rxnormId) &&
                rxnormId.ValueKind == JsonValueKind.Array &&
                rxnormId.GetArrayLength() > 0)
            {
                return rxnormId[0].GetString();
            }

            return null;
        }

        public async Task<IReadOnlyList<string>> GetIngredientRxCuisAsync(string rxcui, CancellationToken cancellationToken = default)
        {
            var url = $"rxcui/{Uri.EscapeDataString(rxcui)}/related.json?tty=IN";
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("relatedGroup", out var relatedGroup))
            {
                return [];
            }

            if (!relatedGroup.TryGetProperty("conceptGroup", out var conceptGroup) || conceptGroup.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var results = new List<string>();
            foreach (var group in conceptGroup.EnumerateArray())
            {
                if (!group.TryGetProperty("conceptProperties", out var props) || props.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var prop in props.EnumerateArray())
                {
                    if (prop.TryGetProperty("rxcui", out var rx) && rx.ValueKind == JsonValueKind.String)
                    {
                        var value = rx.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            results.Add(value);
                        }
                    }
                }
            }

            return results;
        }
    }
}
