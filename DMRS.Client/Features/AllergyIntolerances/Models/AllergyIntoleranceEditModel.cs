using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.AllergyIntolerances.Models;

public sealed class AllergyIntoleranceEditModel
{
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
            allergy.ClinicalStatus = new CodeableConcept { Text = ClinicalStatus };
        }

        return allergy;
    }
}
