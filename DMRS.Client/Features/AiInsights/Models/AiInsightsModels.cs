namespace DMRS.Client.Features.AiInsights.Models;

/// <summary>One scored patient row in a model's population watchlist.</summary>
public sealed record AiPatientRiskRow(
    string PatientId,
    string DisplayName,
    string Href,
    string RiskLevel,
    float Score,
    bool FeaturesComplete,
    string Detail);

/// <summary>
/// Everything the AI Insights page shows for a single ML model: descriptive metadata (so the
/// model catalog stays accurate) plus the live cohort distribution and ranked watchlist.
/// </summary>
public sealed class AiModelCohort
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string Predicts { get; init; }
    public required string Dataset { get; init; }
    public required string Accuracy { get; init; }
    public required string Auc { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
    public required string AccentClass { get; init; }

    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int ImputedCount { get; set; }
    public List<AiPatientRiskRow> Watchlist { get; set; } = [];

    public int Total => HighCount + MediumCount + LowCount;
}

public sealed class ConditionPrevalenceItem
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Full payload for the AI Insights page: one cohort per model plus population health.</summary>
public sealed class AiInsightsSnapshot
{
    public List<AiModelCohort> Models { get; set; } = [];
    public List<ConditionPrevalenceItem> ConditionPrevalence { get; set; } = [];
}
