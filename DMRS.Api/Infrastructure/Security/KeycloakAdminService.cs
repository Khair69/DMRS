using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DMRS.Api.Infrastructure.Security;

public interface IKeycloakAdminService
{
    Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);
}

public sealed class KeycloakAdminService : IKeycloakAdminService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public KeycloakAdminService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Keycloak user id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new ArgumentException("Role name is required.", nameof(roleName));
        }

        var adminToken = await GetAdminAccessTokenAsync(cancellationToken);
        var realm = _configuration["Keycloak:Realm"] ?? "DMRS";
        var adminApiBase = _configuration["Keycloak:AdminApiBaseUrl"] ?? "http://localhost:8080";

        using var roleRequest = new HttpRequestMessage(HttpMethod.Get, $"{adminApiBase.TrimEnd('/')}/admin/realms/{Uri.EscapeDataString(realm)}/roles/{Uri.EscapeDataString(roleName)}");
        roleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var roleResponse = await _httpClient.SendAsync(roleRequest, cancellationToken);
        if (!roleResponse.IsSuccessStatusCode)
        {
            var error = await roleResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Unable to load Keycloak role '{roleName}': {roleResponse.StatusCode} - {error}");
        }

        var roleRepresentation = await roleResponse.Content.ReadFromJsonAsync<KeycloakRoleRepresentation>(cancellationToken: cancellationToken);
        if (roleRepresentation is null || string.IsNullOrWhiteSpace(roleRepresentation.Name))
        {
            throw new InvalidOperationException($"Keycloak role representation for '{roleName}' is invalid.");
        }

        using var assignRequest = new HttpRequestMessage(HttpMethod.Post, $"{adminApiBase.TrimEnd('/')}/admin/realms/{Uri.EscapeDataString(realm)}/users/{Uri.EscapeDataString(userId)}/role-mappings/realm");
        assignRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        assignRequest.Content = JsonContent.Create(new[] { roleRepresentation });

        using var assignResponse = await _httpClient.SendAsync(assignRequest, cancellationToken);
        if (!assignResponse.IsSuccessStatusCode)
        {
            var error = await assignResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Unable to assign role '{roleName}' to Keycloak user '{userId}': {assignResponse.StatusCode} - {error}");
        }
    }

    private async Task<string> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
    {
        var adminRealm = _configuration["Keycloak:AdminRealm"] ?? "master";
        var adminApiBase = _configuration["Keycloak:AdminApiBaseUrl"] ?? "http://localhost:8080";
        var adminClientId = _configuration["Keycloak:AdminClientId"] ?? "admin-cli";
        var adminUsername = _configuration["Keycloak:AdminUsername"] ?? "admin";
        var adminPassword = _configuration["Keycloak:AdminPassword"] ?? "admin";

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{adminApiBase.TrimEnd('/')}/realms/{Uri.EscapeDataString(adminRealm)}/protocol/openid-connect/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = adminClientId,
                ["username"] = adminUsername,
                ["password"] = adminPassword
            })
        };

        using var tokenResponse = await _httpClient.SendAsync(tokenRequest, cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var error = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Unable to get Keycloak admin token: {tokenResponse.StatusCode} - {error}");
        }

        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<KeycloakTokenResponse>(cancellationToken: cancellationToken);
        if (tokenPayload is null || string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
        {
            throw new InvalidOperationException("Keycloak admin token response is invalid.");
        }

        return tokenPayload.AccessToken;
    }

    private sealed class KeycloakTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
    }

    private sealed class KeycloakRoleRepresentation
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool Composite { get; set; }
        public bool ClientRole { get; set; }
        public string? ContainerId { get; set; }
        public Dictionary<string, JsonElement>? Attributes { get; set; }
    }
}
