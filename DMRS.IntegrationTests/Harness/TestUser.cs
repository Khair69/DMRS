using System.Net.Http.Headers;

namespace DMRS.IntegrationTests.Harness;

/// <summary>
/// Describes the principal a request should act as, in terms of the headers
/// <see cref="TestAuthHandler"/> understands. Factory methods mirror the token shapes the real
/// system issues (a practitioner and a patient both carry the realm's default user/system scopes;
/// only the role distinguishes them), so the tests drive the same authorization code paths as
/// production.
/// </summary>
public sealed class TestUser
{
    public const string RolePractitioner = "ROLE_PRACTITIONER";
    public const string RoleOrgAdmin = "ROLE_ORG_ADMIN";
    public const string RoleSystemAdmin = "ROLE_SYSTEM_ADMIN";
    public const string RolePatient = "ROLE_PATIENT";

    public string? Scopes { get; private init; }
    public string? Roles { get; private init; }
    public string? PatientId { get; private init; }
    public string? PractitionerId { get; private init; }
    public string? OrganizationId { get; private init; }
    public string? Subject { get; private init; }

    /// <summary>A staff member (practitioner) optionally bound to an organization.</summary>
    public static TestUser Practitioner(string? practitionerId = null, string? organizationId = null) => new()
    {
        Scopes = "openid profile user/*.* system/*.*",
        Roles = RolePractitioner,
        PractitionerId = practitionerId,
        OrganizationId = organizationId,
        Subject = practitionerId is null ? null : $"kc-prac-{practitionerId}"
    };

    /// <summary>An organization administrator bound to an organization.</summary>
    public static TestUser OrgAdmin(string organizationId) => new()
    {
        Scopes = "openid profile user/*.* system/*.*",
        Roles = RoleOrgAdmin,
        OrganizationId = organizationId,
        Subject = $"kc-orgadmin-{organizationId}"
    };

    /// <summary>
    /// A patient. Deliberately carries the realm's default user/*.* and system/*.* scopes — as a real
    /// patient token does — so the tests confirm the role gate keeps them at patient level.
    /// </summary>
    public static TestUser Patient(string patientId) => new()
    {
        Scopes = "openid profile user/*.* system/*.* patient/*.*",
        Roles = RolePatient,
        PatientId = patientId,
        Subject = $"kc-patient-{patientId}"
    };

    public static TestUser SystemAdmin() => new()
    {
        Scopes = "openid profile user/*.* system/*.*",
        Roles = RoleSystemAdmin,
        Subject = "kc-sysadmin"
    };

    /// <summary>No credentials at all — the request should be rejected with 401.</summary>
    public static TestUser Anonymous() => new();

    public void ApplyTo(HttpRequestMessage request)
    {
        AddHeader(request, TestAuthHandler.ScopesHeader, Scopes);
        AddHeader(request, TestAuthHandler.RolesHeader, Roles);
        AddHeader(request, TestAuthHandler.PatientHeader, PatientId);
        AddHeader(request, TestAuthHandler.PractitionerHeader, PractitionerId);
        AddHeader(request, TestAuthHandler.OrganizationHeader, OrganizationId);
        AddHeader(request, TestAuthHandler.SubjectHeader, Subject);
    }

    private static void AddHeader(HttpRequestMessage request, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }
    }
}
