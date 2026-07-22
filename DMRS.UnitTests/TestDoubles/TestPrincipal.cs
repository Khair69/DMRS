using System.Security.Claims;

namespace DMRS.UnitTests.TestDoubles;

/// <summary>
/// Builds a <see cref="ClaimsPrincipal"/> shaped like a token this API actually receives from
/// Keycloak: SMART scopes in the "scope" claim and realm roles in the "roles" claim (the API
/// configures <c>RoleClaimType = "roles"</c> in Program.cs, so <c>IsInRole</c> reads that claim).
/// </summary>
public sealed class TestPrincipal
{
    public const string RoleSystemAdmin = "ROLE_SYSTEM_ADMIN";
    public const string RoleOrgAdmin = "ROLE_ORG_ADMIN";
    public const string RolePractitioner = "ROLE_PRACTITIONER";
    public const string RolePatient = "ROLE_PATIENT";

    private const string RoleClaimType = "roles";

    private readonly List<Claim> _claims = [];

    public static TestPrincipal Create() => new();

    /// <summary>A staff member: practitioner role plus the realm's default user/system scopes.</summary>
    public static TestPrincipal Practitioner(string? practitionerId = null)
    {
        var principal = Create()
            .WithScopes("openid", "profile", "user/*.*", "system/*.*")
            .WithRoles(RolePractitioner);

        return practitionerId is null ? principal : principal.WithClaim("practitioner", practitionerId);
    }

    /// <summary>
    /// A patient. Note the realm grants user/*.* and system/*.* as DEFAULT client scopes to every
    /// token — including a patient's — so this principal deliberately carries them. The service must
    /// still classify it as patient-level, because it holds no staff role.
    /// </summary>
    public static TestPrincipal Patient(string? patientId = null)
    {
        var principal = Create()
            .WithScopes("openid", "profile", "user/*.*", "system/*.*", "patient/*.*")
            .WithRoles(RolePatient);

        return patientId is null ? principal : principal.WithClaim("patient", patientId);
    }

    public static TestPrincipal SystemAdmin() => Create()
        .WithScopes("openid", "profile", "user/*.*", "system/*.*")
        .WithRoles(RoleSystemAdmin);

    /// <summary>A token with no scope claim at all — e.g. an unauthenticated or malformed caller.</summary>
    public static TestPrincipal Anonymous() => Create();

    public TestPrincipal WithScopes(params string[] scopes)
    {
        _claims.Add(new Claim("scope", string.Join(' ', scopes)));
        return this;
    }

    public TestPrincipal WithRoles(params string[] roles)
    {
        _claims.AddRange(roles.Select(role => new Claim(RoleClaimType, role)));
        return this;
    }

    public TestPrincipal WithClaim(string type, string value)
    {
        _claims.Add(new Claim(type, value));
        return this;
    }

    public ClaimsPrincipal Build()
    {
        var identity = new ClaimsIdentity(_claims, "TestAuth", ClaimTypes.Name, RoleClaimType);
        return new ClaimsPrincipal(identity);
    }

    public static implicit operator ClaimsPrincipal(TestPrincipal builder) => builder.Build();
}
