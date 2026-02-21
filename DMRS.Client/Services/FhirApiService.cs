using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Net.Http;

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
    }

    public async Task<T?> GetResourceAsync<T>(string id) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var response = await _httpClient.GetAsync($"fhir/{resourceType}/{id}");

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        return _deserializer.Deserialize<T>(json);
    }

    public async Task<T?> CreateResourceAsync<T>(T resource) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var json = _serializer.SerializeToString(resource);

        var content = new StringContent(json, Encoding.UTF8, "application/fhir+json");
        var response = await _httpClient.PostAsync($"api/fhir/{resourceType}", content);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return _deserializer.Deserialize<T>(responseJson);
    }

    public async Task<T?> UpdateResourceAsync<T>(string id, T resource) where T : Resource
    {
        var resourceType = typeof(T).Name;
        var json = _serializer.SerializeToString(resource);

        var content = new StringContent(json, Encoding.UTF8, "application/fhir+json");
        var response = await _httpClient.PutAsync($"api/fhir/{resourceType}/{id}", content);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return _deserializer.Deserialize<T>(responseJson);
    }

    public async Task<HttpResponseMessage> TestApi()
    {
        var response = await _httpClient.GetAsync("api/test/test-api");
        return response;
    }
}