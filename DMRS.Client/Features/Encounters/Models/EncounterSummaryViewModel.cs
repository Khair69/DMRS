namespace DMRS.Client.Features.Encounters.Models;

public sealed record EncounterSummaryViewModel(
    string Id,
    string PatientId,
    string Status,
    string ClassCode);
