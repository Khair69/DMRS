using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Conditions.Models;

public sealed class ConditionEditModel
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

    public static ConditionEditModel FromCondition(Condition condition)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(condition.Subject?.Reference, "patient") ?? string.Empty;
        var codeText = condition.Code?.Text ?? condition.Code?.Coding.FirstOrDefault()?.Code ?? string.Empty;
        var clinicalStatus = condition.ClinicalStatus?.Coding.FirstOrDefault()?.Code ?? condition.ClinicalStatus?.Text;

        return new ConditionEditModel
        {
            Id = condition.Id,
            PatientId = patientId,
            CodeText = codeText,
            ClinicalStatus = clinicalStatus
        };
    }

    public Condition ToFhirCondition()
    {
        var condition = new Condition
        {
            Id = Id,
            Code = new CodeableConcept { Text = CodeText }
        };

        var subjectRef = FhirReferenceHelper.NormalizeReference(PatientId, "Patient");
        if (!string.IsNullOrWhiteSpace(subjectRef))
        {
            condition.Subject = new ResourceReference(subjectRef);
        }

        if (!string.IsNullOrWhiteSpace(ClinicalStatus))
        {
            condition.ClinicalStatus = new CodeableConcept { Text = ClinicalStatus };
        }

        return condition;
    }
}
