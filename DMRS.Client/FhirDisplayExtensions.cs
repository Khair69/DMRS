using Hl7.Fhir.Model;

namespace DMRS.Client;

/// <summary>
/// Safe display helpers for FHIR resources that may have been loaded from
/// Synthea (FHIR R4) data. Accessing typed Code&lt;TEnum&gt; properties such as
/// Encounter.Status throws CodedValidationException when the underlying
/// string value is a valid R4 code that was renamed or removed in R5
/// (e.g. "finished" → "completed"). These helpers bypass enum validation
/// by reading JsonValue directly from the underlying element.
/// </summary>
public static class FhirDisplayExtensions
{
    public static string SafeStatus(this Encounter e)
        => e.StatusElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeStatus(this Observation o)
        => o.StatusElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeStatus(this MedicationRequest m)
        => m.StatusElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeIntent(this MedicationRequest m)
        => m.IntentElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafePriority(this MedicationRequest m)
        => m.PriorityElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeStatus(this Procedure p)
        => p.StatusElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeStatus(this Appointment a)
        => a.StatusElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeStatus(this Location l)
        => l.StatusElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeStatus(this ServiceRequest s)
        => s.StatusElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeIntent(this ServiceRequest s)
        => s.IntentElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafePriority(this ServiceRequest s)
        => s.PriorityElement?.JsonValue?.ToString() ?? string.Empty;

    public static string SafeCriticality(this AllergyIntolerance a)
        => a.CriticalityElement?.JsonValue?.ToString() ?? string.Empty;
}
