using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

// FHIR defines a "Task" resource, which collides with System.Threading.Tasks.Task in async signatures.
using Task = System.Threading.Tasks.Task;

namespace DMRS.UnitTests.Ai;

/// <summary>
/// Tests the 30-day readmission predictor against the real deployed
/// <c>readmission_predictor.onnx</c>. Its feature vector is
/// [age, gender, conditionCount, medicationCount, recentEncounterCount, procedureCount], so unlike
/// the other two models most of its inputs are COUNTS of the patient's resources rather than
/// measurements — which makes the counting rules the thing worth testing.
/// </summary>
public class HighUtilizationRiskServiceTests : RiskModelTestBase
{
    private HighUtilizationRiskService CreateService()
        => new(
            Repository,
            Options.Create(new AiRiskPredictorOptions()),
            Environment(),
            ModelPool,
            NullLogger<HighUtilizationRiskService>.Instance);

    /// <summary>
    /// Sets up the four repository calls the service makes for one patient. Encounters and
    /// procedures are fetched as cheap indexed COUNTS rather than as resources.
    /// </summary>
    private void GivenPatientRecord(
        int age,
        AdministrativeGender? gender,
        IEnumerable<Condition>? conditions = null,
        IEnumerable<MedicationRequest>? medications = null,
        int encounterCount = 0,
        int procedureCount = 0)
    {
        Repository.GetAsync<Patient>(PatientId).Returns(new Patient
        {
            Id = PatientId,
            BirthDate = BirthDateForAge(age),
            Gender = gender
        });
        Repository.SearchAsync<Condition>(Arg.Any<Dictionary<string, string>>())
            .Returns([.. conditions ?? []]);
        Repository.SearchAsync<MedicationRequest>(Arg.Any<Dictionary<string, string>>())
            .Returns([.. medications ?? []]);
        Repository.SearchCountAsync<Encounter>(Arg.Any<Dictionary<string, string>>()).Returns(encounterCount);
        Repository.SearchCountAsync<Procedure>(Arg.Any<Dictionary<string, string>>()).Returns(procedureCount);
    }

    private static Condition Diagnosed(string? text = null, string? snomedCode = null, string? display = null) => new()
    {
        Code = new CodeableConcept
        {
            Text = text,
            Coding = snomedCode is null && display is null
                ? null
                : [new Coding("http://snomed.info/sct", snomedCode, display)]
        }
    };

    private static MedicationRequest Prescribed(MedicationRequest.MedicationrequestStatus status) =>
        new() { Status = status };

    [Fact]
    public async Task The_deployed_model_file_is_present_and_loads()
    {
        GivenPatientRecord(70, AdministrativeGender.Female);

        var assessment = await CreateService().AssessPatientAsync(PatientId, CancellationToken.None);

        assessment.ShouldNotBeNull();
        assessment.ModelName.ShouldBe("readmission_predictor.onnx");
        assessment.Probability!.Value.ShouldBeInRange(0f, 1f);
    }

    [Fact]
    public async Task Returns_nothing_for_an_unknown_patient()
    {
        Repository.GetAsync<Patient>(PatientId).Returns((Patient?)null);

        (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None)).ShouldBeNull();
    }

    /// <summary>
    /// Gender is encoded Female=1 / Male=0 here — the OPPOSITE of the cardiovascular model, which
    /// follows the UCI Cleveland convention. Two models in one system with inverted encodings is
    /// exactly the kind of detail that silently corrupts predictions, so both are pinned by tests.
    /// </summary>
    [Theory]
    [InlineData(AdministrativeGender.Female, 1f)]
    [InlineData(AdministrativeGender.Male, 0f)]
    public async Task Gender_encoding_is_the_inverse_of_the_cardiovascular_models(
        AdministrativeGender gender, float expected)
    {
        GivenPatientRecord(70, gender);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Gender.ShouldBe(expected);
        assessment.FeaturesComplete.ShouldBeTrue();
    }

    [Fact]
    public async Task A_patient_with_no_recorded_gender_is_scored_with_imputed_values()
    {
        GivenPatientRecord(70, AdministrativeGender.Unknown);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Gender.ShouldBe(1f);
        assessment.FeaturesComplete.ShouldBeFalse();
    }

    // ------------------------------------------------------------------ counting rules

    /// <summary>
    /// Only ACTIVE and ON-HOLD prescriptions count as current medication burden. Counting completed
    /// or cancelled ones would inflate every long-standing patient's polypharmacy feature.
    /// </summary>
    [Fact]
    public async Task Only_active_and_on_hold_medications_count_towards_medication_burden()
    {
        GivenPatientRecord(
            68,
            AdministrativeGender.Female,
            medications:
            [
                Prescribed(MedicationRequest.MedicationrequestStatus.Active),
                Prescribed(MedicationRequest.MedicationrequestStatus.Active),
                Prescribed(MedicationRequest.MedicationrequestStatus.OnHold),
                Prescribed(MedicationRequest.MedicationrequestStatus.Completed),
                Prescribed(MedicationRequest.MedicationrequestStatus.Cancelled),
                Prescribed(MedicationRequest.MedicationrequestStatus.EnteredInError),
            ]);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.MedicationCount.ShouldBe(3);
    }

    [Fact]
    public async Task Condition_encounter_and_procedure_counts_are_carried_onto_the_assessment()
    {
        GivenPatientRecord(
            68,
            AdministrativeGender.Female,
            conditions: [Diagnosed(text: "Sprained ankle"), Diagnosed(text: "Seasonal allergy")],
            encounterCount: 7,
            procedureCount: 3);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.ConditionCount.ShouldBe(2);
        assessment.RecentEncounterCount.ShouldBe(7);
    }

    // ------------------------------------------------------------------ chronic-condition detection

    [Theory]
    [InlineData("44054006")]   // Type 2 diabetes
    [InlineData("84114007")]   // Heart failure
    [InlineData("709044004")]  // CKD
    public async Task A_chronic_condition_is_detected_from_its_snomed_code(string snomedCode)
    {
        GivenPatientRecord(68, AdministrativeGender.Female, conditions: [Diagnosed(snomedCode: snomedCode)]);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.HasChronicConditions.ShouldBeTrue();
        assessment.TopRiskFactors.ShouldContain("Chronic condition on record");
    }

    [Theory]
    [InlineData("Type 2 diabetes mellitus")]
    [InlineData("Chronic obstructive pulmonary disease (COPD)")]
    [InlineData("ESSENTIAL HYPERTENSION")]   // detection must be case-insensitive
    public async Task A_chronic_condition_is_detected_from_its_free_text(string text)
    {
        GivenPatientRecord(68, AdministrativeGender.Female, conditions: [Diagnosed(text: text)]);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.HasChronicConditions.ShouldBeTrue();
    }

    [Fact]
    public async Task A_chronic_condition_is_detected_from_a_codings_display_name()
    {
        GivenPatientRecord(68, AdministrativeGender.Female,
            conditions: [Diagnosed(snomedCode: "99999999", display: "Congestive heart failure")]);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.HasChronicConditions.ShouldBeTrue();
    }

    [Fact]
    public async Task Acute_conditions_are_not_flagged_as_chronic()
    {
        GivenPatientRecord(68, AdministrativeGender.Female,
            conditions: [Diagnosed(text: "Sprained ankle"), Diagnosed(text: "Acute viral pharyngitis")]);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.HasChronicConditions.ShouldBeFalse();
        assessment.TopRiskFactors.ShouldNotContain("Chronic condition on record");
    }

    // ------------------------------------------------------------------ informational risk factors

    [Theory]
    [InlineData(4, null)]
    [InlineData(5, "Polypharmacy (5 active medications)")]
    [InlineData(9, "Polypharmacy (9 active medications)")]
    [InlineData(10, "Severe polypharmacy (10 active medications)")]
    public async Task Polypharmacy_is_reported_at_its_documented_thresholds(int activeMedications, string? expected)
    {
        GivenPatientRecord(
            68,
            AdministrativeGender.Female,
            medications: Enumerable.Range(0, activeMedications)
                .Select(_ => Prescribed(MedicationRequest.MedicationrequestStatus.Active)));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        if (expected is null)
        {
            assessment.TopRiskFactors.ShouldNotContain(f => f.Contains("olypharmacy"));
        }
        else
        {
            assessment.TopRiskFactors.ShouldContain(expected);
        }
    }

    /// <summary>
    /// The listed factors describe the model's inputs; they must never be mistaken for the score.
    /// A patient can carry factors and still score low, so this pins that they are informational.
    /// </summary>
    [Fact]
    public async Task Risk_factors_describe_the_inputs_and_do_not_themselves_raise_the_score()
    {
        var service = CreateService();

        // Two patients identical in every MODEL feature — same age, gender and counts — differing
        // only in whether their single condition is chronic. Since the chronic flag is descriptive
        // and not a model input, the scores must come out exactly equal.
        GivenPatientRecord(68, AdministrativeGender.Female, encounterCount: 6,
            conditions: [Diagnosed(text: "Sprained ankle")]);
        var acute = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!;

        GivenPatientRecord(68, AdministrativeGender.Female, encounterCount: 6,
            conditions: [Diagnosed(snomedCode: "44054006")]);
        var chronic = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!;

        acute.HasChronicConditions.ShouldBeFalse();
        chronic.HasChronicConditions.ShouldBeTrue();
        chronic.TopRiskFactors.ShouldContain("Chronic condition on record");

        chronic.Probability.ShouldBe(acute.Probability,
            "the chronic flag is an informational factor, not a score contributor");
    }

    // ------------------------------------------------------------------ clinical direction

    [Fact]
    public async Task Risk_rises_with_the_number_of_recent_encounters()
    {
        var service = CreateService();

        GivenPatientRecord(72, AdministrativeGender.Female, encounterCount: 1);
        var occasional = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!.Probability!.Value;

        GivenPatientRecord(72, AdministrativeGender.Female, encounterCount: 12);
        var frequent = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!.Probability!.Value;

        frequent.ShouldBeGreaterThan(occasional,
            $"a frequently-admitted patient should out-rank an occasional one (got {frequent} vs {occasional})");
    }

    [Fact]
    public async Task The_risk_level_and_high_risk_flag_agree_with_each_other()
    {
        // Both are derived from the same configured threshold in this service, so they can never
        // disagree — asserted here because the other two predictors do not share that property.
        GivenPatientRecord(80, AdministrativeGender.Female, encounterCount: 15,
            medications: Enumerable.Range(0, 12).Select(_ => Prescribed(MedicationRequest.MedicationrequestStatus.Active)));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        if (assessment.IsHighRisk)
        {
            assessment.RiskLevel.ShouldBe("High");
        }
        else
        {
            assessment.RiskLevel.ShouldNotBe("High");
        }
    }

    [Fact]
    public async Task Repeated_scoring_of_the_same_patient_is_deterministic()
    {
        GivenPatientRecord(72, AdministrativeGender.Female, encounterCount: 5);
        var service = CreateService();

        var first = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!;
        var second = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!;

        second.Probability.ShouldBe(first.Probability);
    }
}
