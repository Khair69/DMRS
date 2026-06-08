namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    /// <summary>
    /// Result of the diabetes risk model. Feature values are the ones actually fed to the model —
    /// either pulled from the patient's FHIR Observations or median-imputed when missing (listed in
    /// <see cref="ImputedFeatures"/>; <see cref="FeaturesComplete"/> is false when any were imputed).
    /// </summary>
    public sealed record DiabetesRiskAssessment(
        string PatientId,
        float Glucose,
        float BloodPressure,
        float Bmi,
        float Age,
        bool IsHighRisk,
        float? Probability,
        string RiskLevel,
        bool FeaturesComplete,
        string[] ImputedFeatures,
        string ModelName,
        DateTimeOffset EvaluatedAt);
}
