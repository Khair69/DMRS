namespace DMRS.Client.Features.Observations.Models;

public sealed record ObservationSummaryViewModel(
    string Id,
    string PatientId,
    string CodeText,
    string Status);
