using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.IntegrationTests.Harness;

/// <summary>
/// Base class for the integration tests. Provides an <see cref="HttpClient"/> against the hosted API,
/// helpers to send requests as a given <see cref="TestUser"/>, and FHIR resource builders that
/// serialize with the same Firely library the API uses (so payloads are always structurally valid).
/// Each test class resets the database first, keeping classes independent while sharing one container.
/// </summary>
[Collection(IntegrationCollection.Name)]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected const string FhirJson = "application/fhir+json";

    private static readonly FhirJsonSerializer Serializer = new();

    protected DmrsApiFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(DmrsApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await Factory.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ---------------------------------------------------------------- requests

    protected async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string url, TestUser user, Resource? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        user.ApplyTo(request);

        if (body is not null)
        {
            request.Content = new StringContent(Serializer.SerializeToString(body), Encoding.UTF8, "application/json");
        }

        return await Client.SendAsync(request);
    }

    protected Task<HttpResponseMessage> GetAsync(string url, TestUser user)
        => SendAsync(HttpMethod.Get, url, user);

    protected Task<HttpResponseMessage> PostAsync(string url, TestUser user, Resource body)
        => SendAsync(HttpMethod.Post, url, user, body);

    protected Task<HttpResponseMessage> PutAsync(string url, TestUser user, Resource body)
        => SendAsync(HttpMethod.Put, url, user, body);

    protected Task<HttpResponseMessage> DeleteAsync(string url, TestUser user)
        => SendAsync(HttpMethod.Delete, url, user);

    // ---------------------------------------------------------------- response parsing

    protected static async Task<T> ReadResourceAsync<T>(HttpResponseMessage response) where T : Resource
    {
        var json = await response.Content.ReadAsStringAsync();
        return new FhirJsonParser().Parse<T>(json);
    }

    protected static async Task<Bundle> ReadBundleAsync(HttpResponseMessage response)
        => await ReadResourceAsync<Bundle>(response);

    /// <summary>Creates a resource as the given user and returns the server-assigned id.</summary>
    protected async Task<string> CreateAndGetIdAsync(string resourceType, TestUser user, Resource body)
    {
        var response = await PostAsync($"/fhir/{resourceType}", user, body);
        response.EnsureSuccessStatusCode();
        var created = await ReadResourceAsync<Resource>(response);
        return created.Id!;
    }

    // ---------------------------------------------------------------- FHIR builders

    /// <summary>A structurally valid Patient, optionally managed by an organization.</summary>
    protected static Patient NewPatient(
        string? family = "Test",
        string? given = "Patient",
        AdministrativeGender gender = AdministrativeGender.Unknown,
        string? managingOrganizationId = null)
    {
        var patient = new Patient
        {
            Name = { new HumanName { Family = family, Given = given is null ? [] : [given] } },
            Gender = gender,
            BirthDate = "1980-01-01"
        };
        if (managingOrganizationId is not null)
        {
            patient.ManagingOrganization = new ResourceReference($"Organization/{managingOrganizationId}");
        }
        return patient;
    }

    protected static Organization NewOrganization(string name = "Test Clinic")
        => new() { Name = name, Active = true };

    /// <summary>A minimal but valid Observation for a patient (status + code are required in R5).</summary>
    protected static Observation NewObservation(string patientId, double value = 5.5, string loinc = "39156-5")
        => new()
        {
            Status = ObservationStatus.Final,
            Code = new CodeableConcept("http://loinc.org", loinc),
            Subject = new ResourceReference($"Patient/{patientId}"),
            Value = new Quantity((decimal)value, "kg/m2"),
            Effective = new FhirDateTime("2026-01-15")
        };

    protected static JsonElement ToJsonElement(Resource resource)
        => JsonDocument.Parse(Serializer.SerializeToString(resource)).RootElement.Clone();
}
