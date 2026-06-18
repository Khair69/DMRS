namespace DMRS.Shared.Constants;

/// <summary>
/// Canonical FHIR <c>Identifier.system</c> URLs used for the patient business identifiers
/// that DMRS manages itself (as opposed to clinical/MRN identifiers seeded from source data).
/// </summary>
public static class PatientIdentifierSystems
{
    /// <summary>
    /// Short, human-readable patient number in the form <c>XX12345</c> (governorate prefix + 5 digits).
    /// This is the everyday id doctors search/share; the GUID logical id stays internal.
    /// </summary>
    public const string PatientNumber = "https://dmrs.health.sy/fhir/patient-number";

    /// <summary>Syrian national number (11 digits). Seeded by <c>seed-patient-identifiers.sql</c>.</summary>
    public const string NationalId = "https://dmrs.health.sy/fhir/national-id";
}
