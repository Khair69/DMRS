using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Encounters.Models;

public sealed class EncounterEditModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string PatientId { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = "in-progress";

    [Required]
    [MaxLength(100)]
    public string ClassCode { get; set; } = "AMB";

    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }

    public static EncounterEditModel FromEncounter(Encounter encounter)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(encounter.Subject?.Reference, "patient") ?? string.Empty;
        var classCode = encounter.Class?.FirstOrDefault()?.Coding.FirstOrDefault()?.Code ?? string.Empty;

        DateTime? start = null;
        if (DateTime.TryParse(encounter.ActualPeriod?.Start, out var parsedStart))
        {
            start = parsedStart;
        }

        DateTime? end = null;
        if (DateTime.TryParse(encounter.ActualPeriod?.End, out var parsedEnd))
        {
            end = parsedEnd;
        }

        return new EncounterEditModel
        {
            Id = encounter.Id,
            PatientId = patientId,
            Status = encounter.Status?.ToString().ToLowerInvariant() ?? "unknown",
            ClassCode = classCode,
            Start = start,
            End = end
        };
    }

    public Encounter ToFhirEncounter()
    {
        var encounter = new Encounter
        {
            Id = Id,
            Status = ParseStatus(Status),
            Class =
            [
                new CodeableConcept
                {
                    Coding = [new Coding { Code = ClassCode }]
                }
            ]
        };

        var subjectRef = FhirReferenceHelper.NormalizeReference(PatientId, "Patient");
        if (!string.IsNullOrWhiteSpace(subjectRef))
        {
            encounter.Subject = new ResourceReference(subjectRef);
        }

        if (Start.HasValue || End.HasValue)
        {
            encounter.ActualPeriod = new Period
            {
                Start = Start?.ToString("o"),
                End = End?.ToString("o")
            };
        }

        return encounter;
    }

    private static EncounterStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EncounterStatus.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "planned" => EncounterStatus.Planned,
            "arrived" => EncounterStatus.InProgress,
            "triaged" => EncounterStatus.InProgress,
            "in-progress" => EncounterStatus.InProgress,
            "onleave" => EncounterStatus.OnHold,
            "finished" => EncounterStatus.Completed,
            "completed" => EncounterStatus.Completed,
            "cancelled" => EncounterStatus.Cancelled,
            "entered-in-error" => EncounterStatus.EnteredInError,
            _ => EncounterStatus.Unknown
        };
    }
}
