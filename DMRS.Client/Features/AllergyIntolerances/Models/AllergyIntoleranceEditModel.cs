using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.AllergyIntolerances.Models;

public sealed class AllergyIntoleranceEditModel
{
    // AllergyIntolerance.clinicalStatus is bound REQUIRED to the allergyintolerance-clinical value
    // set, so a text-only CodeableConcept is rejected by the server-side validator ("No code found
    // in CodeableConcept with a required binding …") and the create fails with a 400. The status
    // must always be written as a real coding drawn from this system.
    public const string ClinicalStatusSystem = "http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical";

    public static readonly IReadOnlyList<string> ClinicalStatusCodes = ["active", "inactive", "resolved"];

    public string? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string PatientId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CodeText { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ClinicalStatus { get; set; }

    public static AllergyIntoleranceEditModel FromAllergy(AllergyIntolerance allergy)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(allergy.Patient?.Reference, "patient") ?? string.Empty;
        var codeText = allergy.Code?.Text ?? allergy.Code?.Coding.FirstOrDefault()?.Code ?? string.Empty;
        var clinicalStatus = allergy.ClinicalStatus?.Coding.FirstOrDefault()?.Code ?? allergy.ClinicalStatus?.Text;

        return new AllergyIntoleranceEditModel
        {
            Id = allergy.Id,
            PatientId = patientId,
            CodeText = codeText,
            ClinicalStatus = clinicalStatus
        };
    }

    public AllergyIntolerance ToFhirAllergy()
    {
        var allergy = new AllergyIntolerance
        {
            Id = Id,
            Code = new CodeableConcept { Text = CodeText }
        };

        var patientRef = FhirReferenceHelper.NormalizeReference(PatientId, "Patient");
        if (!string.IsNullOrWhiteSpace(patientRef))
        {
            allergy.Patient = new ResourceReference(patientRef);
        }

        if (!string.IsNullOrWhiteSpace(ClinicalStatus))
        {
            allergy.ClinicalStatus = new CodeableConcept(ClinicalStatusSystem, ClinicalStatus.Trim().ToLowerInvariant());
        }

        return allergy;
    }
}
