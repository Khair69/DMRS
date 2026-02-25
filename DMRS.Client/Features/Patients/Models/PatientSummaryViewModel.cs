namespace DMRS.Client.Features.Patients.Models;

public sealed record PatientSummaryViewModel(
    string Id,
    string DisplayName,
    string? Gender,
    string? BirthDate,
    string? Identifier);