using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DMRS.Client.Services;

public class FhirApiService
{
    private readonly HttpClient _httpClient;
    private readonly FhirJsonDeserializer _deserializer;
    private readonly FhirJsonSerializer _serializer;

    public FhirApiService(HttpClient httpClient, FhirJsonDeserializer deserializer, FhirJsonSerializer serializer)
    {
        _httpClient = httpClient;
        _deserializer = deserializer;
        _serializer = serializer;
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/fhir+json"));
    }

    public async Task<T?> GetResourceAsync<T>(string id) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var response = await _httpClient.GetAsync($"fhir/{resourceType}/{id}");

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return _deserializer.Deserialize<T>(json);
    }

    // Reads a FHIR resource from an arbitrary API path (e.g. the patient-portal "me" endpoints,
    // which don't follow the standard fhir/{type}/{id} shape) and deserializes it with the FHIR
    // serializer rather than System.Text.Json.
    public async Task<T?> GetFhirFromPathAsync<T>(string path) where T : Resource
    {
        var response = await _httpClient.GetAsync(path);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return _deserializer.Deserialize<T>(json);
    }

    public async Task<T?> PutFhirToPathAsync<T, TRequest>(string path, TRequest payload) where T : Resource
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/fhir+json"));

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return _deserializer.Deserialize<T>(json);
    }

    public async Task<T?> CreateResourceAsync<T>(T resource) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var json = _serializer.SerializeToString(resource);

        var content = new StringContent(json, Encoding.UTF8, "application/fhir+json");
        var response = await _httpClient.PostAsync($"fhir/{resourceType}", content);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return _deserializer.Deserialize<T>(responseJson);
    }

    public async Task<T?> UpdateResourceAsync<T>(string id, T resource) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var json = _serializer.SerializeToString(resource);

        var content = new StringContent(json, Encoding.UTF8, "application/fhir+json");
        var response = await _httpClient.PutAsync($"fhir/{resourceType}/{id}", content);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return _deserializer.Deserialize<T>(responseJson);
    }

    public async Task<IReadOnlyList<T>> SearchAsync<T>(string searchParam, string value) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var query = new Dictionary<string, string>
        {
            { searchParam, NormalizeSearchValue(searchParam, value) }
        };

        var queryString = QueryHelpers.AddQueryString($"fhir/{resourceType}", query.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value));

        var response = await _httpClient.GetAsync(queryString);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var bundle = _deserializer.Deserialize<Bundle>(json);

        if (bundle?.Entry == null || bundle.Entry.Count == 0)
            return [];

        return bundle.Entry
            .Where(e => e.Resource is T)
            .Select(e => e.Resource)
            .OfType<T>()
            .ToList();
    }

    public async Task<IReadOnlyList<T>> SearchResourcesAsync<T>(Dictionary<string, string>? queryParams = null) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var path = $"fhir/{resourceType}";

        if (queryParams is { Count: > 0 })
        {
            path = QueryHelpers.AddQueryString(path, queryParams.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value));
        }

        var response = await _httpClient.GetAsync(path);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var bundle = _deserializer.Deserialize<Bundle>(json);

        if (bundle?.Entry == null || bundle.Entry.Count == 0)
            return [];

        return bundle.Entry
            .Where(e => e.Resource is T)
            .Select(e => e.Resource)
            .OfType<T>()
            .ToList();
    }

    public async Task<IReadOnlyList<T>> GetHistoryAsync<T>(string id) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var response = await _httpClient.GetAsync($"fhir/{resourceType}/{id}/_history");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        var bundle = _deserializer.Deserialize<Bundle>(json);

        if (bundle?.Entry == null || bundle.Entry.Count == 0)
            return [];

        return bundle.Entry
            .Where(e => e.Resource is T)
            .Select(e => e.Resource)
            .OfType<T>()
            .ToList();
    }

    public async System.Threading.Tasks.Task DeleteResourceAsync<T>(string id) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var response = await _httpClient.DeleteAsync($"fhir/{resourceType}/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<HttpResponseMessage> TestApi()
    {
        var response = await _httpClient.GetAsync("api/test/test-api");
        return response;
    }

    public async Task<TResponse?> PostApiJsonAsync<TRequest, TResponse>(string path, TRequest payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    public async Task<TResponse?> GetApiJsonAsync<TResponse>(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    public async Task<TResponse?> PutApiJsonAsync<TRequest, TResponse>(string path, TRequest payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, path);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    public async System.Threading.Tasks.Task PatchApiAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TResponse?> PostMultipartAsync<TResponse>(string path, MultipartFormDataContent content)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = content;

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    public async System.Threading.Tasks.Task DeleteApiAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task<TResponse?> DeleteApiJsonAsync<TResponse>(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.Accept.Clear();
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    public string GetDownloadUrl(string path) => $"{_httpClient.BaseAddress}{path}";

    // Reference search params (e.g. patient, organization) expect a typed reference
    // like "Patient/123". Let users enter just the bare id and prepend the type here.
    private static string NormalizeSearchValue(string searchParam, string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Contains('/'))
            return trimmed;

        var referenceType = searchParam switch
        {
            "patient" => "Patient",
            "organization" => "Organization",
            _ => null
        };

        return referenceType is null ? trimmed : $"{referenceType}/{trimmed}";
    }

    private IReadOnlyList<T> DeserializeResourceList<T>(string json) where T : Resource
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }
        var resources = new List<T>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            var parsed = _deserializer.Deserialize<T>(element.GetRawText());
            if (parsed is not null)
            {
                resources.Add(parsed);
            }
        }
        return resources;
    }
}
