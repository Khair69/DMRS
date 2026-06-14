using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Security;

/// <summary>
/// Shared helpers for linking a Keycloak account to a Practitioner resource.
/// Used by both the invite-claim flow and the admin-provisioning flow so that a
/// provisioned account is indistinguishable from a self-registered + claimed one.
/// </summary>
public static class StaffAccountLinking
{
    public const string InviteCodeIdentifierSystem = "https://dmrs.local/invites/practitioner";
    public const string OrganizationAdminRoleCode = "ORG_ADMIN";
    public const string DoctorRoleCode = "DOCTOR";
    public const string KeycloakIdentifierSystemFallback = "https://keycloak.local/users";

    public static string BuildKeycloakIdentifierSystem(string? keycloakAuthority)
    {
        if (string.IsNullOrWhiteSpace(keycloakAuthority))
        {
            return KeycloakIdentifierSystemFallback;
        }

        return $"{keycloakAuthority.TrimEnd('/')}/users";
    }

    public static string ResolveRealmRole(IReadOnlyList<PractitionerRole> roles)
    {
        var roleCodes = roles
            .SelectMany(r => r.Code)
            .SelectMany(c => c.Coding)
            .Select(c => c.Code)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim().ToUpperInvariant())
            .ToHashSet();

        if (roleCodes.Contains(OrganizationAdminRoleCode))
        {
            return "ROLE_ORG_ADMIN";
        }

        return "ROLE_PRACTITIONER";
    }

    public static void AddOrReplaceIdentifier(List<Identifier> identifiers, string system, string value)
    {
        var existing = identifiers.FirstOrDefault(i => string.Equals(i.System, system, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            identifiers.Add(new Identifier
            {
                System = system,
                Value = value
            });
            return;
        }

        existing.Value = value;
    }

    /// <summary>
    /// Returns the linked Keycloak user id for a practitioner, or null if no account is linked.
    /// </summary>
    public static string? GetLinkedKeycloakUserId(Practitioner practitioner, string? keycloakAuthority)
    {
        var system = BuildKeycloakIdentifierSystem(keycloakAuthority);
        return practitioner.Identifier?
            .FirstOrDefault(i => string.Equals(i.System, system, StringComparison.OrdinalIgnoreCase))?
            .Value;
    }
}
