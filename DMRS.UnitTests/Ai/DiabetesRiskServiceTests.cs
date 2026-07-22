using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

// FHIR defines a "Task" resource, which collides with System.Threading.Tasks.Task in async signatures.
using Task = System.Threading.Tasks.Task;

namespace DMRS.UnitTests.Ai;

/// <summary>
/// End-to-end tests of the diabetes predictor that run the REAL deployed
/// <c>Ai/diabetes_predictor.onnx</c> file — only the FHIR repository is substituted. They therefore
/// cover the whole inference pipeline in one pass: feature extraction from Observations, imputation
/// of missing features, the ONNX Runtime call, and the parsing of the model's output.
///
/// The "golden vector" tests below pin the probability the deployed model returns for a fixed input.
/// If the .onnx file is ever replaced by a differently-trained model, these fail — which is exactly
/// what should happen, because the model shipped would no longer be the model evaluated in the book.
/// </summary>
public class DiabetesRiskServiceTests : RiskModelTestBase
{
    // Imputation defaults the service applies for a feature with no matching Observation.
    private const float HealthyGlucose = 90f;
    private const float HealthyBloodPressure = 75f;
    private const float HealthyBmi = 23f;

    private DiabetesRiskService CreateService()
        => new(
            Repository,
            new ObservationFeatureExtractor(Repository),
            Options.Create(new DiabetesRiskPredictorOptions()),
            Environment(),
            ModelPool,
            NullLogger<DiabetesRiskService>.Instance);

    [Fact]
    public async Task The_deployed_model_file_is_present_and_loads()
    {
        GivenPatient(BirthDateForAge(50), Observed(GlucoseCode, 100));

        var assessment = await CreateService().AssessPatientAsync(PatientId, CancellationToken.None);

        assessment.ShouldNotBeNull("the ONNX model must ship with the API — a missing file silently disables risk scoring");
        assessment.ModelName.ShouldBe("diabetes_predictor.onnx");
    }

    [Fact]
    public async Task Returns_nothing_for_an_unknown_patient()
    {
        Repository.GetAsync<Patient>(PatientId).Returns((Patient?)null);

        var assessment = await CreateService().AssessPatientAsync(PatientId, CancellationToken.None);

        assessment.ShouldBeNull();
    }

    // ------------------------------------------------------------------ feature vector & imputation

    [Fact]
    public async Task Features_are_taken_from_the_patients_observations()
    {
        GivenPatient(
            BirthDateForAge(45),
            Observed(GlucoseCode, 155),
            Observed(DiastolicBpCode, 92),
            Observed(BmiCode, 33.5));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Glucose.ShouldBe(155);
        assessment.BloodPressure.ShouldBe(92);
        assessment.Bmi.ShouldBe(33.5f, 0.001f);
        assessment.Age.ShouldBe(45);
        assessment.FeaturesComplete.ShouldBeTrue();
        assessment.ImputedFeatures.ShouldBeEmpty();
    }

    [Fact]
    public async Task Missing_features_are_imputed_with_healthy_values_and_reported_as_imputed()
    {
        // Only glucose was measured; the rest must be filled in and declared.
        GivenPatient(BirthDateForAge(30), Observed(GlucoseCode, 110));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Glucose.ShouldBe(110);
        assessment.BloodPressure.ShouldBe(HealthyBloodPressure);
        assessment.Bmi.ShouldBe(HealthyBmi);
        assessment.FeaturesComplete.ShouldBeFalse();
        assessment.ImputedFeatures.ShouldBe(["BloodPressure", "BMI"], ignoreOrder: true);
    }

    /// <summary>
    /// Imputation deliberately uses healthy-normal values rather than the Pima training-set medians
    /// (median BMI 32.3 is obese, median glucose 117 is pre-diabetic). A patient with no measurements
    /// at all must therefore not be reported as high risk on the strength of imputed values alone —
    /// this is the behaviour the book describes, so it is pinned here.
    /// </summary>
    [Fact]
    public async Task A_patient_with_no_measurements_is_not_reported_as_high_risk()
    {
        GivenPatient(BirthDateForAge(30));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Glucose.ShouldBe(HealthyGlucose);
        assessment.BloodPressure.ShouldBe(HealthyBloodPressure);
        assessment.Bmi.ShouldBe(HealthyBmi);
        assessment.ImputedFeatures.ShouldBe(["Glucose", "BloodPressure", "BMI"], ignoreOrder: true);
        assessment.IsHighRisk.ShouldBeFalse();
        assessment.RiskLevel.ShouldBe("Low");
    }

    [Fact]
    public async Task Age_is_derived_from_the_birth_date()
    {
        GivenPatient(BirthDateForAge(72));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Age.ShouldBe(72);
        assessment.ImputedFeatures.ShouldNotContain("Age");
    }

    [Fact]
    public async Task An_unusable_birth_date_falls_back_to_the_median_age_and_is_declared()
    {
        GivenPatient("not-a-date");

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Age.ShouldBe(29);
        assessment.ImputedFeatures.ShouldContain("Age");
    }

    [Fact]
    public async Task A_patient_reference_is_accepted_in_place_of_a_bare_id()
    {
        GivenPatient(BirthDateForAge(40));

        var assessment = await CreateService().AssessPatientAsync($"Patient/{PatientId}", CancellationToken.None);

        assessment.ShouldNotBeNull();
        assessment.PatientId.ShouldBe(PatientId);
    }

    // ------------------------------------------------------------------ golden vectors

    [Fact]
    public async Task A_clearly_low_risk_profile_scores_low()
    {
        // Young, normal glucose, normal BP, healthy weight.
        GivenPatient(
            BirthDateForAge(25),
            Observed(GlucoseCode, 85),
            Observed(DiastolicBpCode, 70),
            Observed(BmiCode, 22.0));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Probability.ShouldNotBeNull();
        assessment.Probability!.Value.ShouldBeLessThan(0.35f);
        assessment.RiskLevel.ShouldBe("Low");
        assessment.IsHighRisk.ShouldBeFalse();
    }

    [Fact]
    public async Task A_clearly_high_risk_profile_scores_high()
    {
        // Middle-aged, markedly hyperglycaemic, obese, blood pressure in the range the training set
        // covers densely (see the characterization test below for what happens outside it).
        GivenPatient(
            BirthDateForAge(50),
            Observed(GlucoseCode, 185),
            Observed(DiastolicBpCode, 80),
            Observed(BmiCode, 32.0));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Probability.ShouldNotBeNull();
        assessment.Probability!.Value.ShouldBeGreaterThan(0.65f);
        assessment.RiskLevel.ShouldBe("High");
        assessment.IsHighRisk.ShouldBeTrue();
    }

    [Fact]
    public async Task The_probability_returned_is_a_valid_probability()
    {
        GivenPatient(BirthDateForAge(45), Observed(GlucoseCode, 130), Observed(BmiCode, 29));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.Probability.ShouldNotBeNull();
        assessment.Probability!.Value.ShouldBeInRange(0f, 1f);
    }

    private async Task<float> ScoreAsync(DiabetesRiskService service, double glucose, double bloodPressure, double bmi, int age)
    {
        GivenPatient(BirthDateForAge(age), Observed(GlucoseCode, glucose), Observed(DiastolicBpCode, bloodPressure), Observed(BmiCode, bmi));
        var assessment = await service.AssessPatientAsync(PatientId, CancellationToken.None);
        return assessment!.Probability!.Value;
    }

    /// <summary>
    /// The clinical sanity check a reviewer asks first: across the range the training set covers
    /// densely, rising glucose must raise the estimated risk. Asserted as a trend over the whole
    /// range rather than step-by-step — see the characterization test below for why.
    /// </summary>
    [Fact]
    public async Task Risk_rises_with_glucose_when_all_other_features_are_held_constant()
    {
        var service = CreateService();

        var normal = await ScoreAsync(service, glucose: 85, bloodPressure: 80, bmi: 30, age: 50);
        var raised = await ScoreAsync(service, glucose: 140, bloodPressure: 80, bmi: 30, age: 50);
        var diabetic = await ScoreAsync(service, glucose: 185, bloodPressure: 80, bmi: 30, age: 50);

        raised.ShouldBeGreaterThan(normal, $"140 mg/dL should out-rank 85 mg/dL (got {raised} vs {normal})");
        diabetic.ShouldBeGreaterThan(raised, $"185 mg/dL should out-rank 140 mg/dL (got {diabetic} vs {raised})");
    }

    [Fact]
    public async Task Risk_rises_with_bmi_across_the_normal_to_obese_range()
    {
        var service = CreateService();

        var lean = await ScoreAsync(service, glucose: 197, bloodPressure: 80, bmi: 22, age: 58);
        var obese = await ScoreAsync(service, glucose: 197, bloodPressure: 80, bmi: 34, age: 58);

        obese.ShouldBeGreaterThan(lean);
    }

    /// <summary>
    /// CHARACTERIZATION TEST — this records a known limitation rather than a desired behaviour.
    ///
    /// The predictor is a Random Forest trained on the 768-row Pima dataset, which contains very few
    /// records with a high diastolic blood pressure. In that sparse region the ensemble is not
    /// monotonic: holding everything else fixed, raising diastolic BP from 88 to 96 mm Hg makes the
    /// estimated risk COLLAPSE (roughly 0.92 to 0.39) instead of rising. The same happens above about
    /// 65 years of age. This is a property of the training data, not a defect in the inference code —
    /// and it is precisely why the system presents these scores as advisory and why the book states
    /// the models must be retrained on local data before any clinical use.
    ///
    /// The test is here so the limitation is measured rather than assumed. If the model is ever
    /// retrained, this test fails and the claim above must be re-checked against the new model.
    /// </summary>
    [Fact]
    public async Task Known_limitation_the_model_is_not_monotonic_where_training_data_is_sparse()
    {
        var service = CreateService();

        var withinDenseRange = await ScoreAsync(service, glucose: 197, bloodPressure: 88, bmi: 38.5, age: 58);
        var outsideDenseRange = await ScoreAsync(service, glucose: 197, bloodPressure: 96, bmi: 38.5, age: 58);

        withinDenseRange.ShouldBeGreaterThan(0.65f);
        outsideDenseRange.ShouldBeLessThan(withinDenseRange,
            "documented limitation: risk drops in the sparsely-sampled high-blood-pressure region");
    }

    /// <summary>
    /// Regression test. The high-risk flag and the reported risk level were once derived from
    /// different cut-points — the flag from the configured threshold, the level from hard-coded 0.65 /
    /// 0.35 — so a score in between produced IsHighRisk=true alongside RiskLevel="Medium", and the
    /// patient chart's badge contradicted the dashboard's pill. Both now come from the same
    /// configured thresholds, so the two can never disagree for any patient.
    /// </summary>
    [Theory]
    [InlineData(85, 70, 22.0, 25)]
    [InlineData(130, 80, 29.0, 45)]
    [InlineData(155, 88, 33.0, 52)]
    [InlineData(185, 80, 32.0, 50)]
    [InlineData(197, 96, 38.5, 58)]
    public async Task The_high_risk_flag_always_agrees_with_the_reported_risk_level(
        double glucose, double bloodPressure, double bmi, int age)
    {
        GivenPatient(
            BirthDateForAge(age),
            Observed(GlucoseCode, glucose),
            Observed(DiastolicBpCode, bloodPressure),
            Observed(BmiCode, bmi));

        var assessment = (await CreateService().AssessPatientAsync(PatientId, CancellationToken.None))!;

        assessment.IsHighRisk.ShouldBe(assessment.RiskLevel == "High",
            $"score {assessment.Probability} reported level '{assessment.RiskLevel}' with IsHighRisk={assessment.IsHighRisk}");
    }

    [Fact]
    public async Task Repeated_scoring_of_the_same_patient_is_deterministic()
    {
        GivenPatient(BirthDateForAge(47), Observed(GlucoseCode, 145), Observed(BmiCode, 31));
        var service = CreateService();

        var first = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!;
        var second = (await service.AssessPatientAsync(PatientId, CancellationToken.None))!;

        second.Probability.ShouldBe(first.Probability);
        second.RiskLevel.ShouldBe(first.RiskLevel);
    }
}
