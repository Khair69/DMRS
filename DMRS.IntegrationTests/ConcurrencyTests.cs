using System.Net;
using DMRS.IntegrationTests.Harness;
using Hl7.Fhir.Model;
using Shouldly;

namespace DMRS.IntegrationTests;

/// <summary>
/// §4-7-5 Multi-user testing — checks the system behaves correctly when many users act at once:
/// data stays isolated between users under concurrent load, and the version history of a record
/// stays consistent even when several updates race for the same resource.
/// </summary>
public class ConcurrencyTests(DmrsApiFactory factory) : IntegrationTestBase(factory)
{
    private static TestUser Admin => TestUser.SystemAdmin();

    [Fact]
    public async Task Sequential_updates_keep_the_version_history_consistent()
    {
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "V1"));

        for (var i = 2; i <= 5; i++)
        {
            var revision = NewPatient(family: $"V{i}");
            revision.Id = id;
            (await PutAsync($"/fhir/Patient/{id}", Admin, revision)).EnsureSuccessStatusCode();
        }

        var history = await ReadBundleAsync(await GetAsync($"/fhir/Patient/{id}/_history", Admin));
        var versionIds = history.Entry.Select(e => int.Parse(e.Resource.Meta.VersionId)).OrderBy(v => v);

        versionIds.ShouldBe([1, 2, 3, 4, 5]);
    }

    /// <summary>
    /// Ten patients each read their own record simultaneously. Every request must return that
    /// caller's own data — never another patient's — proving requests are isolated and no per-user
    /// state bleeds across the concurrent scopes.
    /// </summary>
    [Fact]
    public async Task Concurrent_reads_by_different_patients_stay_isolated()
    {
        var patientIds = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            patientIds.Add(await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: $"Patient{i}")));
        }

        var reads = patientIds.Select(async id =>
        {
            var response = await GetAsync($"/fhir/Patient/{id}", TestUser.Patient(id));
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var patient = await ReadResourceAsync<Patient>(response);
            return (Requested: id, Returned: patient.Id);
        });

        var results = await Task.WhenAll(reads);

        results.ShouldAllBe(r => r.Requested == r.Returned);
    }

    /// <summary>
    /// While many patients concurrently try to read the SAME (first) patient's record, only its owner
    /// succeeds; every other caller is forbidden. This confirms the ownership gate holds under
    /// concurrent contention, not just in isolation.
    /// </summary>
    [Fact]
    public async Task Under_concurrent_access_only_the_owner_can_read_a_record()
    {
        var patientIds = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            patientIds.Add(await CreateAndGetIdAsync("Patient", Admin, NewPatient()));
        }

        var target = patientIds[0];

        var attempts = patientIds.Select(async callerId =>
        {
            var response = await GetAsync($"/fhir/Patient/{target}", TestUser.Patient(callerId));
            return (CallerId: callerId, response.StatusCode);
        });

        var results = await Task.WhenAll(attempts);

        results.Single(r => r.StatusCode == HttpStatusCode.OK).CallerId.ShouldBe(target);
        results.Count(r => r.StatusCode == HttpStatusCode.Forbidden).ShouldBe(9);
    }

    /// <summary>
    /// Several updates race for the same record at once. The version history must stay intact whatever
    /// the interleaving: the optimistic-concurrency token on the resource version means conflicting
    /// writers cannot both commit the same version, so the stored versions remain a gap-free,
    /// duplicate-free chain 1..N. (A losing writer's request surfaces as a server error rather than
    /// silently corrupting the record — that is the property under test here.)
    /// </summary>
    [Fact]
    public async Task Concurrent_updates_to_one_record_never_corrupt_its_version_history()
    {
        const int writers = 6;
        var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: "Contended"));

        var updates = Enumerable.Range(0, writers).Select(async i =>
        {
            var revision = NewPatient(family: $"Writer{i}");
            revision.Id = id;
            var response = await PutAsync($"/fhir/Patient/{id}", Admin, revision);
            return response.StatusCode;
        });

        var statuses = await Task.WhenAll(updates);

        // No writer may produce a "not found" or "bad request" — the only outcomes are success or a
        // concurrency-conflict server error.
        statuses.ShouldAllBe(s => s == HttpStatusCode.OK || s == HttpStatusCode.InternalServerError);
        var successCount = statuses.Count(s => s == HttpStatusCode.OK);
        successCount.ShouldBeGreaterThanOrEqualTo(1);

        var history = await ReadBundleAsync(await GetAsync($"/fhir/Patient/{id}/_history", Admin));
        var versionIds = history.Entry.Select(e => int.Parse(e.Resource.Meta.VersionId)).OrderBy(v => v).ToList();

        // Exactly one version per successful write, plus the original — a contiguous chain with no
        // duplicates and no gaps.
        versionIds.ShouldBe(Enumerable.Range(1, successCount + 1).ToList());
    }
}
