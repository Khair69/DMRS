using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using Hl7.Fhir.Model;
using Shouldly;

namespace DMRS.UnitTests.Ai;

/// <summary>
/// Covers <see cref="ObservationFeatureExtractor.LatestValue"/> — the step that turns a patient's
/// FHIR Observations into the numeric features the risk models consume. Getting this wrong would
/// feed the model a stale or wrong measurement without any visible error, so it is worth pinning
/// down precisely.
/// </summary>
public class ObservationFeatureExtractorTests
{
    private const string BmiCode = "39156-5";
    private const string DiastolicBpCode = "8462-4";
    private const string SystolicBpCode = "8480-6";
    private const string BpPanelCode = "85354-9";

    private static Observation Simple(string loincCode, double value, string? effective = "2026-01-01")
        => new()
        {
            Code = Codeable(loincCode),
            Value = new Quantity((decimal)value, "1"),
            Effective = effective is null ? null : new FhirDateTime(effective)
        };

    private static CodeableConcept Codeable(string code)
        => new() { Coding = [new Coding("http://loinc.org", code)] };

    [Fact]
    public void Returns_null_when_no_observation_matches_the_requested_code()
    {
        var observations = new[] { Simple(BmiCode, 24.5) };

        ObservationFeatureExtractor.LatestValue(observations, DiastolicBpCode).ShouldBeNull();
    }

    [Fact]
    public void Returns_null_for_an_empty_record()
    {
        ObservationFeatureExtractor.LatestValue([], BmiCode).ShouldBeNull();
    }

    [Fact]
    public void Picks_the_most_recent_measurement_not_the_first_encountered()
    {
        var observations = new[]
        {
            Simple(BmiCode, 31.0, "2024-03-01"),
            Simple(BmiCode, 26.5, "2026-05-20"),
            Simple(BmiCode, 28.2, "2025-07-11"),
        };

        ObservationFeatureExtractor.LatestValue(observations, BmiCode).ShouldBe(26.5);
    }

    [Fact]
    public void A_dated_measurement_wins_over_one_with_no_effective_time()
    {
        var observations = new[]
        {
            Simple(BmiCode, 40.0, effective: null),
            Simple(BmiCode, 22.0, "2020-01-01"),
        };

        ObservationFeatureExtractor.LatestValue(observations, BmiCode).ShouldBe(22.0);
    }

    /// <summary>
    /// Blood pressure is recorded as one panel Observation whose own value is empty and whose
    /// systolic/diastolic readings live in components — so a component-blind extractor would silently
    /// report "no blood pressure recorded" for every patient and impute a healthy default instead.
    /// </summary>
    [Fact]
    public void Reads_a_value_carried_in_an_observation_component()
    {
        var panel = new Observation
        {
            Code = Codeable(BpPanelCode),
            Effective = new FhirDateTime("2026-02-14"),
            Component =
            [
                new Observation.ComponentComponent
                {
                    Code = Codeable(SystolicBpCode),
                    Value = new Quantity(138m, "mm[Hg]")
                },
                new Observation.ComponentComponent
                {
                    Code = Codeable(DiastolicBpCode),
                    Value = new Quantity(88m, "mm[Hg]")
                },
            ]
        };

        ObservationFeatureExtractor.LatestValue([panel], DiastolicBpCode).ShouldBe(88);
        ObservationFeatureExtractor.LatestValue([panel], SystolicBpCode).ShouldBe(138);
    }

    [Fact]
    public void Any_of_the_supplied_loinc_codes_may_match()
    {
        // Glucose is reported under several LOINC codes depending on the specimen and fasting state.
        var observations = new[] { Simple("1558-6", 104) };

        ObservationFeatureExtractor.LatestValue(observations, "2339-0", "2345-7", "1558-6").ShouldBe(104);
    }

    [Fact]
    public void Non_numeric_observations_are_ignored()
    {
        var coded = new Observation
        {
            Code = Codeable(BmiCode),
            Value = new CodeableConcept("http://loinc.org", "LA-1"),
            Effective = new FhirDateTime("2026-01-01")
        };

        ObservationFeatureExtractor.LatestValue([coded], BmiCode).ShouldBeNull();
    }

    [Fact]
    public void A_quantity_without_a_value_is_ignored()
    {
        var empty = new Observation
        {
            Code = Codeable(BmiCode),
            Value = new Quantity { Unit = "kg/m2" },
            Effective = new FhirDateTime("2026-01-01")
        };

        ObservationFeatureExtractor.LatestValue([empty], BmiCode).ShouldBeNull();
    }

    [Fact]
    public void An_observation_timed_by_a_period_is_ranked_by_its_start()
    {
        var observations = new[]
        {
            Simple(BmiCode, 30.0, "2023-01-01"),
            new Observation
            {
                Code = Codeable(BmiCode),
                Value = new Quantity(21m, "kg/m2"),
                Effective = new Period { Start = "2026-06-01" }
            },
        };

        ObservationFeatureExtractor.LatestValue(observations, BmiCode).ShouldBe(21);
    }
}
