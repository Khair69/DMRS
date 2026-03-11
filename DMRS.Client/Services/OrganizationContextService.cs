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

    public OrganizationContextService(AuthenticationStateProvider authenticationStateProvider)
    {
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<IReadOnlyList<string>> GetOrganizationIdsAsync()
    {
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

        return organizations.ToList();
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
