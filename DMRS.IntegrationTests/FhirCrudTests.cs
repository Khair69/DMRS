using System.Net;
using DMRS.IntegrationTests.Harness;
using Hl7.Fhir.Model;
using Shouldly;

namespace DMRS.IntegrationTests;

/// <summary>
/// §4-7-1 Functional testing — exercises the full FHIR resource lifecycle end to end against the real
/// controllers, repository and PostgreSQL: create, read, search, update (with version history),
/// version-specific read, and soft delete. Every request runs through the genuine authorization
/// pipeline; a system admin provisions data so the tests focus on the data operations themselves.
/// </summary>
public class FhirCrudTests(DmrsApiFactory factory) : IntegrationTestBase(factory)
{
    private static TestUser Admin => TestUser.SystemAdmin();

    [Fact]
    public async Task A_created_resource_can_be_read_back()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin,
            NewPatient(family: "Ibrahim", given: "Sara", gender: AdministrativeGender.Female));

        var response = await GetAsync($"/fhir/Patient/{id}", Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var patient = await ReadResourceAsync<Patient>(response);
        patient.Id.ShouldBe(id);
        patient.Name[0].Family.ShouldBe("Ibrahim");
        patient.Gender.ShouldBe(AdministrativeGender.Female);
    }

    [Fact]
    public async Task A_newly_created_resource_starts_at_version_1()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient());

        var response = await GetAsync($"/fhir/Patient/{id}", Admin);
        var patient = await ReadResourceAsync<Patient>(response);

        patient.Meta.VersionId.ShouldBe("1");
    }

    [Fact]
    public async Task Updating_a_resource_bumps_the_version_and_persists_the_change()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Original"));

        var updated = NewPatient(family: "Corrected");
        updated.Id = id;
        var updateResponse = await PutAsync($"/fhir/Patient/{id}", Admin, updated);
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var readResponse = await GetAsync($"/fhir/Patient/{id}", Admin);
        var patient = await ReadResourceAsync<Patient>(readResponse);
        patient.Name[0].Family.ShouldBe("Corrected");
        patient.Meta.VersionId.ShouldBe("2");
    }

    [Fact]
    public async Task Each_update_adds_an_entry_to_the_version_history()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "V1"));

        foreach (var family in new[] { "V2", "V3" })
        {
            var revision = NewPatient(family: family);
            revision.Id = id;
            (await PutAsync($"/fhir/Patient/{id}", Admin, revision)).EnsureSuccessStatusCode();
        }

        var response = await GetAsync($"/fhir/Patient/{id}/_history", Admin);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var history = await ReadBundleAsync(response);
        history.Type.ShouldBe(Bundle.BundleType.History);
        history.Entry.Count.ShouldBe(3);

        // History is newest-first; the version ids must be the full 3, 2, 1 with no gaps.
        var versionIds = history.Entry.Select(e => e.Resource.Meta.VersionId).ToArray();
        versionIds.ShouldBe(["3", "2", "1"]);
    }

    [Fact]
    public async Task A_specific_past_version_can_be_read_and_reflects_that_versions_state()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Before"));
        var updated = NewPatient(family: "After");
        updated.Id = id;
        (await PutAsync($"/fhir/Patient/{id}", Admin, updated)).EnsureSuccessStatusCode();

        var v1 = await ReadResourceAsync<Patient>(await GetAsync($"/fhir/Patient/{id}/_history/1", Admin));
        var v2 = await ReadResourceAsync<Patient>(await GetAsync($"/fhir/Patient/{id}/_history/2", Admin));

        v1.Name[0].Family.ShouldBe("Before");
        v2.Name[0].Family.ShouldBe("After");
    }

    [Fact]
    public async Task A_deleted_resource_can_no_longer_be_read()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient());

        var deleteResponse = await DeleteAsync($"/fhir/Patient/{id}", Admin);
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var readResponse = await GetAsync($"/fhir/Patient/{id}", Admin);
        readResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_deleted_resource_no_longer_appears_in_search_results()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Deletable"));
        await DeleteAsync($"/fhir/Patient/{id}", Admin);

        var response = await GetAsync("/fhir/Patient?family=Deletable", Admin);
        var bundle = await ReadBundleAsync(response);

        bundle.Entry.ShouldNotContain(e => e.Resource.Id == id);
    }

    [Fact]
    public async Task Deleting_an_already_missing_resource_returns_not_found()
    {
        var response = await DeleteAsync("/fhir/Patient/does-not-exist", Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_clinical_resource_can_be_created_against_a_patient_and_found_by_subject()
    {
        var patientId = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Chart"));
        var observationId = await CreateAndGetIdAsync("Observation", Admin, NewObservation(patientId, value: 27.4));

        var response = await GetAsync($"/fhir/Observation?patient=Patient/{patientId}", Admin);
        var bundle = await ReadBundleAsync(response);

        bundle.Entry.ShouldContain(e => e.Resource.Id == observationId);
    }
}
