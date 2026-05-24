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
        bool FeaturesComplete,
        int ConditionCount = 0,
        int MedicationCount = 0,
        int RecentEncounterCount = 0,
        bool HasChronicConditions = false,
        float CompositeScore = 0f,
        string RiskLevel = "Unknown",
        string[] TopRiskFactors = null!);
}
