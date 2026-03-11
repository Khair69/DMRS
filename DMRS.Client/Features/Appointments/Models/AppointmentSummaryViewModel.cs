namespace DMRS.Client.Features.Appointments.Models;

public sealed record AppointmentSummaryViewModel(
    string Id,
    string PatientId,
    string Status,
    string? Start);
