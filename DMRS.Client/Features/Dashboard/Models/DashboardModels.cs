namespace DMRS.Client.Features.Dashboard.Models;

public sealed record DashboardMetricModel(
    string Label,
    string Value,
    string Helper,
    string AccentClass,
    string Href);

public sealed record DashboardWatchlistItemModel(
    string PatientId,
    string DisplayName,
    string Summary,
    string Href,
    bool IsHighRisk,
    float? Probability,
    string RiskLevel = "Unknown",
    float CompositeScore = 0f,
    int ConditionCount = 0,
    int MedicationCount = 0,
    int RecentEncounterCount = 0,
    bool HasChronicConditions = false,
    List<string>? TopRiskFactors = null);

public sealed record DashboardActivityItemModel(
    string Title,
    string Subtitle,
    string Meta,
    string Href);

public sealed class DashboardSnapshotModel
{
    public List<DashboardMetricModel> Metrics { get; set; } = [];
    public List<DashboardWatchlistItemModel> HighRiskPatients { get; set; } = [];
    public List<DashboardActivityItemModel> UpcomingAppointments { get; set; } = [];
    public List<DashboardActivityItemModel> RecentMedicationRequests { get; set; } = [];
    public int ActiveRuleCount { get; set; }
    public int DraftRuleCount { get; set; }
}

public sealed class HighUtilizationRiskAssessmentModel
{
    public string PatientId { get; set; } = string.Empty;
    public float Age { get; set; }
    public float Gender { get; set; }
    public bool IsHighRisk { get; set; }
    public float? Probability { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; set; }
    public bool FeaturesComplete { get; set; }

    // Clinical composite score fields
    public int ConditionCount { get; set; }
    public int MedicationCount { get; set; }
    public int RecentEncounterCount { get; set; }
    public bool HasChronicConditions { get; set; }
    public float CompositeScore { get; set; }
    public string RiskLevel { get; set; } = "Unknown";
    public List<string> TopRiskFactors { get; set; } = [];
}
