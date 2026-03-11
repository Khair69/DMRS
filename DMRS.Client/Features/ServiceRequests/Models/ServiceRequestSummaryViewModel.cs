namespace DMRS.Client.Features.ServiceRequests.Models;

public sealed record ServiceRequestSummaryViewModel(
    string Id,
    string PatientId,
    string CodeText,
    string Status,
    string Intent);
