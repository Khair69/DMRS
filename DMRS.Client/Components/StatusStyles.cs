namespace DMRS.Client.Components;

/// <summary>
/// Shared mapping from FHIR status codes to the pill colour and the localization key used to label
/// them. The codes come from a dozen different value sets (event, request, encounter, appointment,
/// clinical status …) but they read the same way to a clinician, so the whole workspace colours them
/// with one table rather than a per-page switch.
/// </summary>
public static class StatusStyles
{
    public static string PillClass(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "active" or "completed" or "final" or "finished" or "fulfilled" or "resolved"
            or "booked" or "arrived" or "confirmed" => "dmrs-pill--success",

        "draft" or "planned" or "proposed" or "pending" or "preparation" or "preliminary"
            or "registered" or "in-progress" or "triaged" or "onleave" or "on-hold"
            or "amended" or "recurrence" or "relapse" or "remission" => "dmrs-pill--warning",

        "stopped" or "cancelled" or "revoked" or "not-done" or "noshow" or "suspended"
            or "entered-in-error" or "refuted" or "error" => "dmrs-pill--danger",

        _ => "dmrs-pill--neutral"
    };

    /// <summary>
    /// Localization key for a status code — <c>on-hold</c> becomes <c>common.status.onHold</c>,
    /// matching the keys the medication and appointment forms already use. Callers fall back to the
    /// raw code when the key is missing (LocalizationService returns the key unchanged).
    /// </summary>
    public static string LabelKey(string status)
    {
        var parts = status.Trim().ToLowerInvariant().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var camel = parts[0] + string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        return $"common.status.{camel}";
    }
}
