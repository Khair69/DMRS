using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication.Internal;

namespace DMRS.Client.Services;

public sealed class KeycloakClaimsPrincipalFactory : AccountClaimsPrincipalFactory<RemoteUserAccount>
{
    public KeycloakClaimsPrincipalFactory(IAccessTokenProviderAccessor accessor)
        : base(accessor)
    {
    }

    public override async ValueTask<ClaimsPrincipal> CreateUserAsync(
        RemoteUserAccount account,
        RemoteAuthenticationUserOptions options)
    {
        var user = await base.CreateUserAsync(account, options);
        if (user.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return user;
        }

        var roleClaimType = string.IsNullOrWhiteSpace(options.RoleClaim) ? "roles" : options.RoleClaim;
        var normalizedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in identity.FindAll(roleClaimType).ToList())
        {
            if (!TryParseJsonArray(claim.Value, out var parsedRoles))
            {
                normalizedRoles.Add(claim.Value);
                continue;
            }

            identity.RemoveClaim(claim);
            foreach (var role in parsedRoles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    normalizedRoles.Add(role);
                }
            }
        }

        if (account.AdditionalProperties.TryGetValue("realm_access", out var realmAccessObj)
            && TryGetRealmRoles(realmAccessObj, out var realmRoles))
        {
            foreach (var role in realmRoles)
            {
                normalizedRoles.Add(role);
            }
        }

        foreach (var role in normalizedRoles)
        {
            if (!identity.HasClaim(roleClaimType, role))
            {
                identity.AddClaim(new Claim(roleClaimType, role));
            }
        }

        return user;
    }

    private static bool TryGetRealmRoles(object? value, out IReadOnlyList<string> roles)
    {
        roles = [];
        if (value is not JsonElement element || element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        roles = rolesElement
            .EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Cast<string>()
            .ToArray();

        return roles.Count > 0;
    }

    private static bool TryParseJsonArray(string input, out IReadOnlyList<string> values)
    {
        values = [];
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (!trimmed.StartsWith('[') || !trimmed.EndsWith(']'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            values = document.RootElement
                .EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Cast<string>()
                .ToArray();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
