namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record HighUtilizationRiskAssessment(
        string PatientId,
        float Age,
        float Gender,
        bool IsHighRisk,
        float? Probability,
        string ModelName,
        DateTimeOffset EvaluatedAt,
        bool FeaturesComplete);
}
