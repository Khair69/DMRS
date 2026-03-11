namespace DMRS.Client.Features.Procedures.Models;

public sealed record ProcedureSummaryViewModel(
    string Id,
    string PatientId,
    string CodeText,
    string Status);
