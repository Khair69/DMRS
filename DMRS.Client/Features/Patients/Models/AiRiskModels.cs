namespace DMRS.Client.Features.Patients.Models;

/// <summary>Client mirror of the API's DiabetesRiskAssessment record.</summary>
public sealed class DiabetesRiskAssessmentModel
{
    public string PatientId { get; set; } = string.Empty;
    public float Glucose { get; set; }
    public float BloodPressure { get; set; }
    public float Bmi { get; set; }
    public float Age { get; set; }
    public bool IsHighRisk { get; set; }
    public float? Probability { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public bool FeaturesComplete { get; set; }
    public List<string> ImputedFeatures { get; set; } = [];
    public string ModelName { get; set; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; set; }
}

/// <summary>Client mirror of the API's CardiovascularRiskAssessment record.</summary>
public sealed class CardiovascularRiskAssessmentModel
{
    public string PatientId { get; set; } = string.Empty;
    public float Age { get; set; }
    public float Sex { get; set; }
    public float RestingBloodPressure { get; set; }
    public float Cholesterol { get; set; }
    public float MaxHeartRate { get; set; }
    public float FastingBloodSugar { get; set; }
    public bool IsHighRisk { get; set; }
    public float? Probability { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public bool FeaturesComplete { get; set; }
    public List<string> ImputedFeatures { get; set; } = [];
    public string ModelName { get; set; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; set; }
}
