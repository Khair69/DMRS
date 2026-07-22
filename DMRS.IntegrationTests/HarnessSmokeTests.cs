using System.Net;
using DMRS.IntegrationTests.Harness;
using Hl7.Fhir.Model;
using Shouldly;

namespace DMRS.IntegrationTests;

/// <summary>
/// Confirms the harness itself works: the container starts, migrations apply, the API hosts, the test
/// authentication scheme is wired in, and a resource round-trips through the real repository.
/// </summary>
public class HarnessSmokeTests(DmrsApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task An_anonymous_request_is_rejected()
    {
        var response = await GetAsync("/fhir/Patient/does-not-exist", TestUser.Anonymous());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_system_admin_can_create_and_a_practitioner_can_read_a_patient()
    {
        // A system admin provisions the record (system level bypasses org ownership); a practitioner
        // may then read any patient across organizations.
        var id = await CreateAndGetIdAsync("Patient", TestUser.SystemAdmin(), NewPatient(family: "Smoke"));

        var response = await GetAsync($"/fhir/Patient/{id}", TestUser.Practitioner());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patient = await ReadResourceAsync<Patient>(response);
        patient.Name[0].Family.ShouldBe("Smoke");
    }
}
