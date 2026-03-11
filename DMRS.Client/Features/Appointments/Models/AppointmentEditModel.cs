using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Appointments.Models;

public sealed class AppointmentEditModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string PatientId { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = "booked";

    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }

    public static AppointmentEditModel FromAppointment(Appointment appointment)
    {
        var patientId = appointment.Participant
            .Select(p => FhirReferenceHelper.ExtractReferenceId(p.Actor?.Reference, "patient"))
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? string.Empty;

        DateTime? start = null;
        if (appointment.StartElement?.Value is DateTimeOffset startDto)
        {
            start = startDto.UtcDateTime;
        }
        else if (DateTime.TryParse(appointment.StartElement?.Value?.ToString(), out var parsedStart))
        {
            start = parsedStart;
        }

        DateTime? end = null;
        if (appointment.EndElement?.Value is DateTimeOffset endDto)
        {
            end = endDto.UtcDateTime;
        }
        else if (DateTime.TryParse(appointment.EndElement?.Value?.ToString(), out var parsedEnd))
        {
            end = parsedEnd;
        }

        return new AppointmentEditModel
        {
            Id = appointment.Id,
            PatientId = patientId,
            Status = appointment.Status?.ToString().ToLowerInvariant() ?? "unknown",
            Start = start,
            End = end
        };
    }

    public Appointment ToFhirAppointment()
    {
        var appointment = new Appointment
        {
            Id = Id,
            Status = ParseStatus(Status)
        };

        var patientRef = FhirReferenceHelper.NormalizeReference(PatientId, "Patient");
        if (!string.IsNullOrWhiteSpace(patientRef))
        {
            appointment.Participant =
            [
                new Appointment.ParticipantComponent
                {
                    Actor = new ResourceReference(patientRef),
                    Status = Appointment.ParticipationStatus.Accepted
                }
            ];
        }

        if (Start.HasValue)
        {
            var startUtc = DateTime.SpecifyKind(Start.Value, DateTimeKind.Utc);
            appointment.StartElement = new Instant(new DateTimeOffset(startUtc));
        }

        if (End.HasValue)
        {
            var endUtc = DateTime.SpecifyKind(End.Value, DateTimeKind.Utc);
            appointment.EndElement = new Instant(new DateTimeOffset(endUtc));
        }

        return appointment;
    }

    private static Appointment.AppointmentStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Appointment.AppointmentStatus.Pending;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "proposed" => Appointment.AppointmentStatus.Proposed,
            "pending" => Appointment.AppointmentStatus.Pending,
            "booked" => Appointment.AppointmentStatus.Booked,
            "arrived" => Appointment.AppointmentStatus.Arrived,
            "fulfilled" => Appointment.AppointmentStatus.Fulfilled,
            "cancelled" => Appointment.AppointmentStatus.Cancelled,
            "noshow" => Appointment.AppointmentStatus.Noshow,
            "entered-in-error" => Appointment.AppointmentStatus.EnteredInError,
            _ => Appointment.AppointmentStatus.Pending
        };
    }
}
