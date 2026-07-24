using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Conditions.Models;

public sealed class ConditionEditModel
{
    // Condition.clinicalStatus is bound REQUIRED to the condition-clinical value set, so a text-only
    // CodeableConcept is rejected by the server-side validator ("No code found in CodeableConcept
    // with a required binding …") and the create fails with a 400. The status must always be written
    // as a real coding drawn from this system.
    public const string ClinicalStatusSystem = "http://terminology.hl7.org/CodeSystem/condition-clinical";

    public static readonly IReadOnlyList<string> ClinicalStatusCodes =
        ["active", "recurrence", "relapse", "inactive", "remission", "resolved"];

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
            condition.ClinicalStatus = new CodeableConcept(ClinicalStatusSystem, ClinicalStatus.Trim().ToLowerInvariant());
        }

        return condition;
    }
}
