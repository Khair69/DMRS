namespace DMRS.Client.Features.AllergyIntolerances.Models;

public sealed record AllergyIntoleranceSummaryViewModel(
    string Id,
    string PatientId,
    string CodeText,
    string? ClinicalStatus);
