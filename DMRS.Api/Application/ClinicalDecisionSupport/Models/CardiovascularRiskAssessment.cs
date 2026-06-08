namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    /// <summary>
    /// Result of the cardiovascular (heart disease) risk model. Feature values are the ones actually
    /// fed to the model — pulled from the patient's FHIR data or median-imputed when missing (listed
    /// in <see cref="ImputedFeatures"/>; <see cref="FeaturesComplete"/> is false when any were imputed).
    /// </summary>
    public sealed record CardiovascularRiskAssessment(
        string PatientId,
        float Age,
        float Sex,
        float RestingBloodPressure,
        float Cholesterol,
        float MaxHeartRate,
        float FastingBloodSugar,
        bool IsHighRisk,
        float? Probability,
        string RiskLevel,
        bool FeaturesComplete,
        string[] ImputedFeatures,
        string ModelName,
        DateTimeOffset EvaluatedAt);
}
