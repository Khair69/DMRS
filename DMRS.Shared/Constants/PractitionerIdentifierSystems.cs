namespace DMRS.Shared.Constants;

/// <summary>
/// Canonical FHIR <c>Identifier.system</c> URLs offered when a staff member (FHIR <c>Practitioner</c>)
/// is invited. The national number and passport reuse the same systems as patients — the identifier
/// namespace belongs to the document, not to the resource type it is attached to.
/// </summary>
public static class PractitionerIdentifierSystems
{
    /// <summary>Syrian national number (11 digits).</summary>
    public const string NationalId = PatientIdentifierSystems.NationalId;

    /// <summary>Passport number, for staff identified by passport rather than national number.</summary>
    public const string Passport = PatientIdentifierSystems.Passport;

    /// <summary>Medical syndicate / practice licence number.</summary>
    public const string MedicalLicense = "https://dmrs.health.sy/fhir/medical-license";
}
