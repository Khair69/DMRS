using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DMRS.IntegrationTests.Harness;

/// <summary>
/// Replaces the real Keycloak JWT bearer authentication in the integration host. Each request
/// carries the principal it wants to act as in request headers, and this handler turns them into the
/// exact claims the production token would have (SMART scopes in "scope", realm roles in "roles",
/// plus the patient/practitioner/organization launch claims). This lets the tests exercise the real
/// authorization pipeline — FhirScopeHandler, SmartAuthorizationService, the controllers — without a
/// running identity server, while keeping the claim shape identical to production.
///
/// A request with no auth headers is treated as anonymous (no ticket), so the [Authorize] pipeline
/// returns 401 exactly as it would for a missing bearer token.
/// </summary>
public sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "IntegrationTest";

    public const string ScopesHeader = "X-Test-Scopes";
    public const string RolesHeader = "X-Test-Roles";
    public const string PatientHeader = "X-Test-Patient";
    public const string PractitionerHeader = "X-Test-Practitioner";
    public const string OrganizationHeader = "X-Test-Organization";
    public const string SubjectHeader = "X-Test-Subject";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var request = Request;

        var hasAnyAuthHeader =
            request.Headers.ContainsKey(ScopesHeader) ||
            request.Headers.ContainsKey(RolesHeader) ||
            request.Headers.ContainsKey(SubjectHeader);

        if (!hasAnyAuthHeader)
        {
            // No identity supplied — behave like a request with no bearer token.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();

        if (request.Headers.TryGetValue(ScopesHeader, out var scopes) && scopes.Count > 0)
        {
            claims.Add(new Claim("scope", scopes.ToString()));
        }

        if (request.Headers.TryGetValue(RolesHeader, out var roles))
        {
            foreach (var role in roles.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim("roles", role));
            }
        }

        AddSingle(claims, "patient", request, PatientHeader);
        AddSingle(claims, "practitioner", request, PractitionerHeader);
        AddSingle(claims, "organization", request, OrganizationHeader);
        AddSingle(claims, "sub", request, SubjectHeader);

        // The production JWT setup uses RoleClaimType = "roles" and NameClaimType = "preferred_username".
        var identity = new ClaimsIdentity(claims, SchemeName, "preferred_username", "roles");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static void AddSingle(List<Claim> claims, string claimType, HttpRequest request, string header)
    {
        if (request.Headers.TryGetValue(header, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            claims.Add(new Claim(claimType, value.ToString()));
        }
    }
}
