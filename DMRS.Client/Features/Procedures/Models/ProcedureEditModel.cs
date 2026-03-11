using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Procedures.Models;

public sealed class ProcedureEditModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string PatientId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CodeText { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = "completed";

    public DateTime? PerformedDateTime { get; set; }

    public static ProcedureEditModel FromProcedure(Procedure procedure)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(procedure.Subject?.Reference, "patient") ?? string.Empty;
        var codeText = procedure.Code?.Text ?? procedure.Code?.Coding.FirstOrDefault()?.Code ?? string.Empty;
        var status = procedure.Status?.ToString().ToLowerInvariant() ?? "unknown";

        DateTime? performed = null;
        if (procedure.Occurrence is FhirDateTime dateTime && DateTime.TryParse(dateTime.Value, out var parsed))
        {
            performed = parsed;
        }

        return new ProcedureEditModel
        {
            Id = procedure.Id,
            PatientId = patientId,
            CodeText = codeText,
            Status = status,
            PerformedDateTime = performed
        };
    }

    public Procedure ToFhirProcedure()
    {
        var procedure = new Procedure
        {
            Id = Id,
            Status = ParseStatus(Status),
            Code = new CodeableConcept { Text = CodeText }
        };

        var subjectRef = FhirReferenceHelper.NormalizeReference(PatientId, "Patient");
        if (!string.IsNullOrWhiteSpace(subjectRef))
        {
            procedure.Subject = new ResourceReference(subjectRef);
        }

        if (PerformedDateTime.HasValue)
        {
            procedure.Occurrence = new FhirDateTime(PerformedDateTime.Value);
        }

        return procedure;
    }

    private static EventStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EventStatus.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "preparation" => EventStatus.Preparation,
            "in-progress" => EventStatus.InProgress,
            "not-done" => EventStatus.NotDone,
            "on-hold" => EventStatus.OnHold,
            "stopped" => EventStatus.Stopped,
            "completed" => EventStatus.Completed,
            "entered-in-error" => EventStatus.EnteredInError,
            _ => EventStatus.Unknown
        };
    }
}
