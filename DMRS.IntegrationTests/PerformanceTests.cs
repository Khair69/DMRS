using System.Diagnostics;
using System.Text.Json;
using DMRS.IntegrationTests.Harness;
using Shouldly;
using Xunit.Abstractions;

namespace DMRS.IntegrationTests;

/// <summary>
/// §4-7-6 Performance testing — measures the batch risk-scoring path the book highlights. The AI
/// Insights dashboard scores a whole cohort with ONE request instead of calling the per-patient
/// endpoint once per patient; this test seeds a cohort, times both approaches against the running
/// API, and asserts the batch path is the faster one. The measured numbers are printed so they can be
/// quoted directly in the report.
/// </summary>
public class PerformanceTests(DmrsApiFactory factory, ITestOutputHelper output) : IntegrationTestBase(factory)
{
    private const int CohortSize = 40;
    private static TestUser Admin => TestUser.SystemAdmin();

    [Fact]
    public async Task Batch_risk_scoring_is_faster_than_scoring_each_patient_individually()
    {
        // Seed a cohort, each patient with a couple of observations so scoring has real features.
        var patientIds = new List<string>();
        for (var i = 0; i < CohortSize; i++)
        {
            var id = await CreateAndGetIdAsync("Patient", Admin, NewPatient(family: $"Cohort{i}"));
            await CreateAndGetIdAsync("Observation", Admin, NewObservation(id, value: 120 + i, loinc: "2339-0")); // glucose
            await CreateAndGetIdAsync("Observation", Admin, NewObservation(id, value: 28 + (i % 10), loinc: "39156-5")); // BMI
            patientIds.Add(id);
        }

        // Warm up both paths so the model load and JIT are not charged to the timed run.
        (await GetAsync("/cds/risk/diabetes/batch", Admin)).EnsureSuccessStatusCode();
        (await GetAsync($"/cds/risk/diabetes/{patientIds[0]}", Admin)).EnsureSuccessStatusCode();

        // Timed: one batch request scoring the whole cohort.
        var batchTimer = Stopwatch.StartNew();
        var batchResponse = await GetAsync("/cds/risk/diabetes/batch", Admin);
        batchTimer.Stop();
        batchResponse.EnsureSuccessStatusCode();

        var scored = JsonDocument.Parse(await batchResponse.Content.ReadAsStringAsync()).RootElement.GetArrayLength();

        // Timed: the per-patient endpoint called once per patient, as a client without batching would.
        var perPatientTimer = Stopwatch.StartNew();
        foreach (var id in patientIds)
        {
            (await GetAsync($"/cds/risk/diabetes/{id}", Admin)).EnsureSuccessStatusCode();
        }
        perPatientTimer.Stop();

        output.WriteLine($"Cohort size:            {CohortSize} patients");
        output.WriteLine($"Batch (1 request):      {batchTimer.ElapsedMilliseconds} ms");
        output.WriteLine($"Per-patient ({CohortSize} requests): {perPatientTimer.ElapsedMilliseconds} ms");
        output.WriteLine($"Speed-up:               {(double)perPatientTimer.ElapsedMilliseconds / Math.Max(1, batchTimer.ElapsedMilliseconds):F1}x");

        // Correctness: the batch scored the whole cohort in that single request.
        scored.ShouldBe(CohortSize);

        // The headline claim: consolidating the cohort into one request is faster than N calls.
        batchTimer.ElapsedMilliseconds.ShouldBeLessThan(perPatientTimer.ElapsedMilliseconds);
    }
}
