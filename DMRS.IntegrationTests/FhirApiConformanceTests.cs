using System.Net;
using DMRS.IntegrationTests.Harness;
using Hl7.Fhir.Model;
using Shouldly;

namespace DMRS.IntegrationTests;

/// <summary>
/// §4-7-2 REST API testing — checks that the API honours the HTTP and FHIR contract: correct status
/// codes, the Location and ETag headers on create, the <c>application/fhir+json</c> content type,
/// FHIR Bundle shapes for searches, and <c>OperationOutcome</c> bodies for validation failures.
/// </summary>
public class FhirApiConformanceTests(DmrsApiFactory factory) : IntegrationTestBase(factory)
{
    private static TestUser Admin => TestUser.SystemAdmin();

    [Fact]
    public async Task Creating_a_resource_returns_201_with_a_location_header_and_a_versioned_etag()
    {
        var response = await PostAsync("/fhir/Patient", Admin, NewPatient(family: "Created"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await ReadResourceAsync<Patient>(response);
        response.Headers.Location!.ToString().ShouldBe($"/fhir/Patient/{created.Id}");

        // The weak ETag must carry the version a client would send back as If-Match on a later update.
        response.Headers.ETag.ShouldNotBeNull();
        response.Headers.ETag.IsWeak.ShouldBeTrue();
        response.Headers.ETag.Tag.ShouldBe("\"1\"");

        // The created resource is returned with its server-assigned version metadata.
        created.Meta.VersionId.ShouldBe("1");
    }

    [Fact]
    public async Task Reading_a_resource_returns_the_fhir_json_content_type()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient());

        var response = await GetAsync($"/fhir/Patient/{id}", Admin);

        response.Content.Headers.ContentType!.MediaType.ShouldBe(FhirJson);
    }

    [Fact]
    public async Task Reading_a_missing_resource_returns_404()
    {
        var response = await GetAsync("/fhir/Patient/no-such-id", Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_search_returns_a_fhir_searchset_bundle()
    {
        await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Bundleish"));

        var response = await GetAsync("/fhir/Patient?family=Bundleish", Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(FhirJson);

        var bundle = await ReadBundleAsync(response);
        bundle.Type.ShouldBe(Bundle.BundleType.Searchset);
        bundle.Total.ShouldBe(bundle.Entry.Count);
        bundle.Entry.ShouldAllBe(e => e.FullUrl != null);
    }

    [Fact]
    public async Task Search_filters_by_the_supplied_parameter()
    {
        await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Mansour"));
        await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Haddad"));

        var response = await GetAsync("/fhir/Patient?family=Mansour", Admin);
        var bundle = await ReadBundleAsync(response);

        bundle.Entry.Count.ShouldBe(1);
        bundle.Entry[0].Resource.ShouldBeOfType<Patient>().Name[0].Family.ShouldBe("Mansour");
    }

    [Fact]
    public async Task A_search_matching_nothing_returns_an_empty_bundle_not_an_error()
    {
        var response = await GetAsync("/fhir/Patient?family=NobodyHasThisName", Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bundle = await ReadBundleAsync(response);
        bundle.Total.ShouldBe(0);
        bundle.Entry.ShouldBeEmpty();
    }

    [Fact]
    public async Task Posting_an_invalid_resource_returns_400_with_a_fhir_operation_outcome()
    {
        // A structurally valid JSON Observation that violates FHIR R5's cardinality rules: status is
        // required (1..1). It deserializes cleanly, then fails validation — the path that returns the
        // validation OperationOutcome.
        var invalid = NewObservation("some-patient");
        invalid.Status = null;

        var response = await PostAsync("/fhir/Observation", Admin, invalid);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The error must be a real FHIR OperationOutcome a client can parse — same media type as the
        // success paths — not a System.Text.Json dump of the .NET object graph.
        response.Content.Headers.ContentType!.MediaType.ShouldBe(FhirJson);

        var outcome = await ReadResourceAsync<OperationOutcome>(response);
        outcome.Issue.ShouldNotBeEmpty();
        outcome.Issue.ShouldContain(i => i.Severity == OperationOutcome.IssueSeverity.Error);
    }

    [Fact]
    public async Task Updating_with_a_mismatched_body_id_is_rejected()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient());

        var mismatched = NewPatient(family: "Wrong");
        mismatched.Id = "a-different-id";
        var response = await PutAsync($"/fhir/Patient/{id}", Admin, mismatched);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_a_resource_that_does_not_exist_returns_404()
    {
        var patient = NewPatient();
        patient.Id = "ghost";

        var response = await PutAsync("/fhir/Patient/ghost", Admin, patient);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_bad_version_identifier_is_rejected()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient());

        var response = await GetAsync($"/fhir/Patient/{id}/_history/not-a-number", Admin);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
