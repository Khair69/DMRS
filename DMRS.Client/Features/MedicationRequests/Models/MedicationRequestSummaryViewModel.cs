namespace DMRS.Client.Features.MedicationRequests.Models;

public sealed record MedicationRequestSummaryViewModel(
    string Id,
    string PatientId,
    string MedicationText,
    string Status,
    string Intent);
