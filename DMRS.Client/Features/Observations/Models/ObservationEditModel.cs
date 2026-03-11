using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Observations.Models;

public sealed class ObservationEditModel
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
    public string Status { get; set; } = "final";

    public DateTime? EffectiveDateTime { get; set; }

    [MaxLength(500)]
    public string? ValueText { get; set; }

    public static ObservationEditModel FromObservation(Observation observation)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(observation.Subject?.Reference, "patient") ?? string.Empty;
        var codeText = observation.Code?.Text ?? observation.Code?.Coding.FirstOrDefault()?.Code ?? string.Empty;
        var status = observation.Status?.ToString().ToLowerInvariant() ?? "unknown";

        DateTime? effective = null;
        if (observation.Effective is FhirDateTime dateTime && DateTime.TryParse(dateTime.Value, out var parsed))
        {
            effective = parsed;
        }

        var valueText = observation.Value is FhirString textValue ? textValue.Value : null;

        return new ObservationEditModel
        {
            Id = observation.Id,
            PatientId = patientId,
            CodeText = codeText,
            Status = status,
            EffectiveDateTime = effective,
            ValueText = valueText
        };
    }

    public Observation ToFhirObservation()
    {
        var observation = new Observation
        {
            Id = Id,
            Status = ParseStatus(Status),
            Code = new CodeableConcept { Text = CodeText }
        };

        var subjectRef = FhirReferenceHelper.NormalizeReference(PatientId, "Patient");
        if (!string.IsNullOrWhiteSpace(subjectRef))
        {
            observation.Subject = new ResourceReference(subjectRef);
        }

        if (EffectiveDateTime.HasValue)
        {
            observation.Effective = new FhirDateTime(EffectiveDateTime.Value);
        }

        if (!string.IsNullOrWhiteSpace(ValueText))
        {
            observation.Value = new FhirString(ValueText);
        }

        return observation;
    }

    private static ObservationStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ObservationStatus.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "registered" => ObservationStatus.Registered,
            "preliminary" => ObservationStatus.Preliminary,
            "final" => ObservationStatus.Final,
            "amended" => ObservationStatus.Amended,
            "cancelled" => ObservationStatus.Cancelled,
            _ => ObservationStatus.Unknown
        };
    }
}
