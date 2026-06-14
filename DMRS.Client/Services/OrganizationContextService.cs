using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace DMRS.Client.Services;

public sealed class OrganizationContextService
{
    private static readonly string[] OrganizationIdClaimTypes =
    [
        "organization",
        "organization_id",
        "org_id",
        "launch_organization",
        "launch/organization"
    ];

    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly FhirApiService _fhirApiService;

    // Cache the resolved ids for the lifetime of this scoped service so multiple components on the
    // same page (nav + dashboard) don't each hit the API.
    private IReadOnlyList<string>? _cachedOrganizationIds;

    public OrganizationContextService(
        AuthenticationStateProvider authenticationStateProvider,
        FhirApiService fhirApiService)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _fhirApiService = fhirApiService;
    }

    public async Task<IReadOnlyList<string>> GetOrganizationIdsAsync()
    {
        if (_cachedOrganizationIds is not null)
        {
            return _cachedOrganizationIds;
        }

        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var organizations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claimType in OrganizationIdClaimTypes)
        {
            foreach (var claim in user.FindAll(claimType))
            {
                foreach (var value in SplitClaimValues(claim.Value))
                {
                    var organizationId = ParseReferenceId(value, "organization");
                    if (!string.IsNullOrWhiteSpace(organizationId))
                    {
                        organizations.Add(organizationId);
                    }
                }
            }
        }

        // Staff org membership is usually not a token claim — it lives in FHIR data and is resolved
        // server-side (Keycloak user → Practitioner → PractitionerRole → Organization). Fall back to
        // the API when the token carries no organization claim.
        if (organizations.Count == 0)
        {
            try
            {
                var resolved = await _fhirApiService.GetApiJsonAsync<List<string>>("api/me/organizations");
                if (resolved is not null)
                {
                    foreach (var organizationId in resolved)
                    {
                        if (!string.IsNullOrWhiteSpace(organizationId))
                        {
                            organizations.Add(organizationId);
                        }
                    }
                }
            }
            catch
            {
                // Leave organizations empty on failure; callers surface the "no organization" state.
            }
        }

        _cachedOrganizationIds = organizations.ToList();
        return _cachedOrganizationIds;
    }

    public async Task<string?> GetPrimaryOrganizationIdAsync()
    {
        var organizationIds = await GetOrganizationIdsAsync();
        return organizationIds.FirstOrDefault();
    }

    private static IEnumerable<string> SplitClaimValues(string claimValue)
    {
        return claimValue.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string? ParseReferenceId(string value, string expectedResourceType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var prefix = $"{expectedResourceType}/";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[prefix.Length..];
        }

        if (trimmed.Contains('/'))
        {
            return null;
        }

        return trimmed;
    }
}
