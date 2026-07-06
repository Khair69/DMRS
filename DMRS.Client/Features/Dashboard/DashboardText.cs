using DMRS.Client.Features.Dashboard.Models;
using DMRS.Client.Services;

namespace DMRS.Client.Features.Dashboard;

/// <summary>
/// Render-time localization for dashboard watchlist rows. The risk level and the composed
/// "risk · detail" summary are built here — rather than baked into the snapshot on the server —
/// so they follow live language switches like the rest of the UI.
/// </summary>
public static class DashboardText
{
    /// <summary>Localized risk-level label for the pill (High/Medium/Low/Unknown).</summary>
    public static string RiskLevel(LocalizationService loc, string riskLevel) => riskLevel switch
    {
        "High" => loc["dashboard.level.high"],
        "Medium" => loc["dashboard.level.medium"],
        "Low" => loc["dashboard.level.low"],
        _ => loc["dashboard.level.unknown"],
    };

    /// <summary>Localized "&lt;risk&gt; · &lt;detail&gt;" watchlist subtitle.</summary>
    public static string WatchlistSummary(LocalizationService loc, DashboardWatchlistItemModel item)
    {
        if (!item.FeaturesComplete)
        {
            return loc["dashboard.watchlist.missingData"];
        }

        var parts = new List<string>();
        if (item.ConditionCount > 0) parts.Add(loc["dashboard.watchlist.conditions", item.ConditionCount]);
        if (item.MedicationCount > 0) parts.Add(loc["dashboard.watchlist.meds", item.MedicationCount]);
        if (item.RecentEncounterCount > 0) parts.Add(loc["dashboard.watchlist.visits", item.RecentEncounterCount]);
        var detail = parts.Count > 0
            ? string.Join(" · ", parts)
            : loc["dashboard.watchlist.age", (int)item.Age];

        var risk = item.RiskLevel switch
        {
            "High" => loc["dashboard.watchlist.riskHigh"],
            "Medium" => loc["dashboard.watchlist.riskMedium"],
            "Low" => loc["dashboard.watchlist.riskLow"],
            _ => loc["dashboard.watchlist.riskUnknown"],
        };

        return loc["dashboard.watchlist.summary", risk, detail];
    }
}
