using DMRS.Api.Application;
using Hl7.Fhir.Model;
using Shouldly;

namespace DMRS.UnitTests.Fhir;

/// <summary>
/// AllergyIntolerance.clinicalStatus and Condition.clinicalStatus carry REQUIRED bindings, so a
/// CodeableConcept holding only free text is rejected by the validator and the write fails with a
/// 400. The client edit models used to build exactly that, which made "create allergy" unusable.
/// These tests pin the contract the client mappers have to satisfy.
/// </summary>
public class ClinicalStatusBindingTests
{
    private static readonly FhirValidatorService Validator = new();

    [Fact]
    public async System.Threading.Tasks.Task Allergy_with_text_only_clinical_status_is_rejected()
    {
        var allergy = new AllergyIntolerance
        {
            Patient = new ResourceReference("Patient/abc"),
            Code = new CodeableConcept { Text = "Penicillin" },
            ClinicalStatus = new CodeableConcept { Text = "active" }
        };

        var outcome = await Validator.ValidateAsync(allergy);

        outcome.Success.ShouldBeFalse();
        outcome.Issue.ShouldContain(i => i.Details.Text.Contains("required binding"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Allergy_with_coded_clinical_status_is_accepted()
    {
        var allergy = new AllergyIntolerance
        {
            Patient = new ResourceReference("Patient/abc"),
            Code = new CodeableConcept { Text = "Penicillin" },
            ClinicalStatus = new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", "active")
        };

        var outcome = await Validator.ValidateAsync(allergy);

        outcome.Success.ShouldBeTrue(outcome.ToString());
    }

    [Fact]
    public async System.Threading.Tasks.Task Condition_with_text_only_clinical_status_is_rejected()
    {
        var condition = new Condition
        {
            Subject = new ResourceReference("Patient/abc"),
            Code = new CodeableConcept { Text = "Hypertension" },
            ClinicalStatus = new CodeableConcept { Text = "active" }
        };

        var outcome = await Validator.ValidateAsync(condition);

        outcome.Success.ShouldBeFalse();
        outcome.Issue.ShouldContain(i => i.Details.Text.Contains("required binding"));
    }

    [Fact]
    public async System.Threading.Tasks.Task Condition_with_coded_clinical_status_is_accepted()
    {
        var condition = new Condition
        {
            Subject = new ResourceReference("Patient/abc"),
            Code = new CodeableConcept { Text = "Hypertension" },
            ClinicalStatus = new CodeableConcept(
                "http://terminology.hl7.org/CodeSystem/condition-clinical", "active")
        };

        var outcome = await Validator.ValidateAsync(condition);

        outcome.Success.ShouldBeTrue(outcome.ToString());
    }
}
