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
    float? Probability);

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
}
