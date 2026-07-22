using System.Net;
using DMRS.IntegrationTests.Harness;
using Hl7.Fhir.Model;
using Shouldly;

namespace DMRS.IntegrationTests;

/// <summary>
/// §4-7-3 Authorization testing (end to end) — complements the fast unit tests of the authorization
/// logic by driving the whole running stack, including the real PostgreSQL ownership lookups. It
/// proves the guarantees the book states: a patient reaches only their own record, organization
/// boundaries hold for writes and administrative reads, staff may read across organizations, and an
/// unauthenticated caller is rejected outright.
/// </summary>
public class AuthorizationIntegrationTests(DmrsApiFactory factory) : IntegrationTestBase(factory)
{
    private static TestUser Admin => TestUser.SystemAdmin();

    // ---------------------------------------------------------------- authentication

    [Fact]
    public async Task An_unauthenticated_request_is_rejected_with_401()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient());

        var response = await GetAsync($"/fhir/Patient/{id}", TestUser.Anonymous());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---------------------------------------------------------------- patient isolation

    [Fact]
    public async Task A_patient_can_read_their_own_record()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Owner"));

        var response = await GetAsync($"/fhir/Patient/{id}", TestUser.Patient(id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The headline guarantee, proven against the live database: a signed-in patient who edits the id
    /// in the URL to point at another patient's record is refused. Note the patient's token carries the
    /// realm's default user/*.* scope, so this also confirms end to end that the role gate keeps them
    /// at patient level rather than elevating them to an organization caller.
    /// </summary>
    [Fact]
    public async Task A_patient_cannot_read_another_patients_record()
    {
        var mine = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Mine"));
        var theirs = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Theirs"));

        var response = await GetAsync($"/fhir/Patient/{theirs}", TestUser.Patient(mine));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_patient_can_read_their_own_observation_but_not_another_patients()
    {
        var mine = await CreateAndGetIdAsync("Patient", Admin, NewPatient());
        var theirs = await CreateAndGetIdAsync("Patient", Admin, NewPatient());
        var myObservation = await CreateAndGetIdAsync("Observation", Admin, NewObservation(mine));
        var theirObservation = await CreateAndGetIdAsync("Observation", Admin, NewObservation(theirs));

        var readMine = await GetAsync($"/fhir/Observation/{myObservation}", TestUser.Patient(mine));
        var readTheirs = await GetAsync($"/fhir/Observation/{theirObservation}", TestUser.Patient(mine));

        readMine.StatusCode.ShouldBe(HttpStatusCode.OK);
        readTheirs.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_patient_cannot_create_a_clinical_resource_for_another_patient()
    {
        var mine = await CreateAndGetIdAsync("Patient", Admin, NewPatient());
        var theirs = await CreateAndGetIdAsync("Patient", Admin, NewPatient());

        var forThemselves = await PostAsync("/fhir/Observation", TestUser.Patient(mine), NewObservation(mine));
        var forSomeoneElse = await PostAsync("/fhir/Observation", TestUser.Patient(mine), NewObservation(theirs));

        forThemselves.StatusCode.ShouldBe(HttpStatusCode.Created);
        forSomeoneElse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_patients_search_only_returns_their_own_data()
    {
        var mine = await CreateAndGetIdAsync("Patient", Admin, NewPatient());
        var theirs = await CreateAndGetIdAsync("Patient", Admin, NewPatient());
        var myObservation = await CreateAndGetIdAsync("Observation", Admin, NewObservation(mine, value: 21));
        await CreateAndGetIdAsync("Observation", Admin, NewObservation(theirs, value: 21));

        var response = await GetAsync("/fhir/Observation", TestUser.Patient(mine));
        var bundle = await ReadBundleAsync(response);

        bundle.Entry.Select(e => e.Resource.Id).ShouldBe([myObservation]);
    }

    // ---------------------------------------------------------------- staff cross-organization read

    [Fact]
    public async Task A_practitioner_may_read_a_patient_from_any_organization()
    {
        var orgId = await CreateAndGetIdAsync("Organization", Admin, NewOrganization("Other Clinic"));
        var patientId = await CreateAndGetIdAsync("Patient", Admin, NewPatient(managingOrganizationId: orgId));

        // The practitioner belongs to no organization at all, yet may still read the patient record.
        var response = await GetAsync($"/fhir/Patient/{patientId}", TestUser.Practitioner());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- organization boundaries

    [Fact]
    public async Task An_org_admin_can_delete_a_patient_of_their_own_org_but_not_another_orgs()
    {
        var orgA = await CreateAndGetIdAsync("Organization", Admin, NewOrganization("Org A"));
        var orgB = await CreateAndGetIdAsync("Organization", Admin, NewOrganization("Org B"));
        var patientOfA = await CreateAndGetIdAsync("Patient", Admin, NewPatient(managingOrganizationId: orgA));
        var patientOfB = await CreateAndGetIdAsync("Patient", Admin, NewPatient(managingOrganizationId: orgB));

        var deleteForeign = await DeleteAsync($"/fhir/Patient/{patientOfB}", TestUser.OrgAdmin(orgA));
        var deleteOwn = await DeleteAsync($"/fhir/Patient/{patientOfA}", TestUser.OrgAdmin(orgA));

        deleteForeign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        deleteOwn.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_org_admin_cannot_read_another_organizations_administrative_record()
    {
        var orgA = await CreateAndGetIdAsync("Organization", Admin, NewOrganization("Org A"));
        var orgB = await CreateAndGetIdAsync("Organization", Admin, NewOrganization("Org B"));

        var readOwn = await GetAsync($"/fhir/Organization/{orgA}", TestUser.OrgAdmin(orgA));
        var readForeign = await GetAsync($"/fhir/Organization/{orgB}", TestUser.OrgAdmin(orgA));

        readOwn.StatusCode.ShouldBe(HttpStatusCode.OK);
        readForeign.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
