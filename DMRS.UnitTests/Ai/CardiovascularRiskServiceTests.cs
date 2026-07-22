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
/// Tests the cardiovascular predictor against the real deployed <c>cardiovascular_predictor.onnx</c>.
/// Its feature vector is [age, sex, trestbps, chol, thalach, fbs] — trained on the UCI Cleveland
/// dataset, so sex is encoded male=1 / female=0 and fbs is a derived flag rather than a raw value.
/// </summary>
public class CardiovascularRiskServiceTests : RiskModelTestBase
{
    // Imputation defaults the service applies when a feature has no matching Observation.
    private const float HealthyRestingBp = 120f;
    private const float HealthyCholesterol = 180f;

    private CardiovascularRiskService CreateService()
        => new(
            Repository,
            new ObservationFeatureExtractor(Repository),
            Options.Create(new CardiovascularRiskPredictorOptions()),
            Environment(),
            ModelPool,
            NullLogger<CardiovascularRiskService>.Instance);

    private void GivenPatient(int age, AdministrativeGender? gender, params Observation[] observations)
        => GivenPatient(
            new Patient { Id = PatientId, BirthDate = BirthDateForAge(age), Gender = gender },
            observations);

    [Fact]
    public async Task The_deployed_model_file_is_present_and_loads()
    {
        GivenPatient(60, AdministrativeGender.Male);

        var assessment = await CreateService().AssessPatientAsync(PatientId, CancellationToken.None);

        assessment.ShouldNotBeNull();
        assessment.ModelName.ShouldBe("cardiovascular_predictor.onnx");
        assessment.Probability!.Value.ShouldBeInRange(0f, 1f);
    }

    [Fact]
    public async Task Returns_nothing_for_an_unknown_patient()
    {
        Repository.GetAsync<Patient>(PatientId).Returns((Patient?)null);

        (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None)).ShouldBeNull();
    }

    // ------------------------------------------------------------------ feature vector

    [Fact]
    public async Task Features_are_taken_from_the_patients_record()
    {
        GivenPatient(
            61,
            AdministrativeGender.Male,
            Observed(SystolicBpCode, 148),
            Observed(CholesterolCode, 268),
            Observed(HeartRateCode, 122),
            Observed(GlucoseCode, 135));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Age.ShouldBe(61);
        assessment.RestingBloodPressure.ShouldBe(148);
        assessment.Cholesterol.ShouldBe(268);
        assessment.MaxHeartRate.ShouldBe(122);
        assessment.FeaturesComplete.ShouldBeTrue();
    }

    /// <summary>
    /// Sex uses the UCI Cleveland encoding, male=1 / female=0 — deliberately the OPPOSITE of the
    /// high-utilization model's encoding. Getting this backwards would not throw; it would silently
    /// invert the risk estimate for every patient, so it is pinned explicitly.
    /// </summary>
    [Theory]
    [InlineData(AdministrativeGender.Male, 1f)]
    [InlineData(AdministrativeGender.Female, 0f)]
    public async Task Sex_uses_the_uci_cleveland_encoding(AdministrativeGender gender, float expected)
    {
        GivenPatient(55, gender);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Sex.ShouldBe(expected);
        assessment.ImputedFeatures.ShouldNotContain("sex");
    }

    [Theory]
    [InlineData(AdministrativeGender.Unknown)]
    [InlineData(AdministrativeGender.Other)]
    [InlineData(null)]
    public async Task An_unrecorded_sex_is_imputed_and_declared(AdministrativeGender? gender)
    {
        GivenPatient(55, gender);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.ImputedFeatures.ShouldContain("sex");
        assessment.FeaturesComplete.ShouldBeFalse();
    }

    /// <summary>
    /// fbs is "fasting blood sugar &gt; 120 mg/dL" — a derived 1/0 flag, not the glucose value itself.
    /// </summary>
    [Theory]
    [InlineData(95, 0f)]
    [InlineData(120, 0f)]   // boundary: strictly greater than 120 is required
    [InlineData(121, 1f)]
    [InlineData(180, 1f)]
    public async Task Fasting_blood_sugar_is_derived_as_a_flag_from_the_glucose_observation(
        double glucose, float expectedFlag)
    {
        GivenPatient(55, AdministrativeGender.Male, Observed(GlucoseCode, glucose));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.FastingBloodSugar.ShouldBe(expectedFlag);
        assessment.ImputedFeatures.ShouldNotContain("fbs");
    }

    // ------------------------------------------------------------------ imputation

    [Fact]
    public async Task Missing_clinical_features_are_imputed_with_healthy_values_and_declared()
    {
        GivenPatient(45, AdministrativeGender.Female);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.RestingBloodPressure.ShouldBe(HealthyRestingBp);
        assessment.Cholesterol.ShouldBe(HealthyCholesterol);
        assessment.FastingBloodSugar.ShouldBe(0f);
        assessment.FeaturesComplete.ShouldBeFalse();
        assessment.ImputedFeatures.ShouldBe(["trestbps", "chol", "thalach", "fbs"], ignoreOrder: true);
    }

    /// <summary>
    /// Unlike the other imputed defaults, max heart rate is not a single cohort median — it is the
    /// age-predicted maximum (220 − age), so it scales with the patient instead of making a 25-year-old
    /// and a 75-year-old look identical on that feature.
    /// </summary>
    [Theory]
    [InlineData(30, 190f)]
    [InlineData(70, 150f)]
    public async Task Missing_max_heart_rate_is_imputed_from_the_patients_age(int age, float expected)
    {
        GivenPatient(age, AdministrativeGender.Male);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.MaxHeartRate.ShouldBe(expected);
        assessment.ImputedFeatures.ShouldContain("thalach");
    }

    [Fact]
    public async Task An_unusable_birth_date_falls_back_to_the_median_age_and_is_declared()
    {
        GivenPatient(new Patient { Id = PatientId, BirthDate = "unknown", Gender = AdministrativeGender.Male });

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Age.ShouldBe(56);
        assessment.ImputedFeatures.ShouldContain("age");
    }

    // ------------------------------------------------------------------ clinical direction

    [Fact]
    public async Task A_healthy_young_patient_with_no_records_is_not_reported_as_high_risk()
    {
        GivenPatient(28, AdministrativeGender.Female);

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.IsHighRisk.ShouldBeFalse(
            "imputing healthy-normal values must not make a record-less young patient look diseased");
    }

    [Fact]
    public async Task Risk_rises_with_cholesterol_when_all_other_features_are_held_constant()
    {
        var service = CreateService();

        GivenPatient(58, AdministrativeGender.Male, Observed(SystolicBpCode, 130), Observed(CholesterolCode, 170), Observed(HeartRateCode, 150));
        var desirable = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!.Probability!.Value;

        GivenPatient(58, AdministrativeGender.Male, Observed(SystolicBpCode, 130), Observed(CholesterolCode, 300), Observed(HeartRateCode, 150));
        var raised = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!.Probability!.Value;

        raised.ShouldBeGreaterThan(desirable, $"raised cholesterol should out-rank desirable (got {raised} vs {desirable})");
    }

    /// <summary>
    /// Regression test — see the equivalent in the diabetes tests. The high-risk flag and the reported
    /// level once used different cut-points, so a score between them made the patient chart's badge
    /// contradict the dashboard's pill. Both now derive from the same configured thresholds.
    /// </summary>
    [Theory]
    [InlineData(28, AdministrativeGender.Female, 110, 170, 170)]
    [InlineData(45, AdministrativeGender.Male, 130, 210, 155)]
    [InlineData(58, AdministrativeGender.Male, 130, 300, 150)]
    [InlineData(61, AdministrativeGender.Male, 148, 268, 122)]
    [InlineData(70, AdministrativeGender.Female, 160, 320, 110)]
    public async Task The_high_risk_flag_always_agrees_with_the_reported_risk_level(
        int age, AdministrativeGender gender, double restingBp, double cholesterol, double maxHeartRate)
    {
        GivenPatient(
            age,
            gender,
            Observed(SystolicBpCode, restingBp),
            Observed(CholesterolCode, cholesterol),
            Observed(HeartRateCode, maxHeartRate));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.IsHighRisk.ShouldBe(assessment.RiskLevel == "High",
            $"score {assessment.Probability} reported level '{assessment.RiskLevel}' with IsHighRisk={assessment.IsHighRisk}");
    }

    [Fact]
    public async Task Repeated_scoring_of_the_same_patient_is_deterministic()
    {
        GivenPatient(58, AdministrativeGender.Male, Observed(CholesterolCode, 240));
        var service = CreateService();

        var first = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!;
        var second = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!;

        second.Probability.ShouldBe(first.Probability);
    }
}
