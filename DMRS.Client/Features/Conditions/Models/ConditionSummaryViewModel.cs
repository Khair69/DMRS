namespace DMRS.Client.Features.Conditions.Models;

public sealed record ConditionSummaryViewModel(
    string Id,
    string PatientId,
    string CodeText,
    string? ClinicalStatus);
