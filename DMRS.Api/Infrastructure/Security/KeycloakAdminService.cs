using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DMRS.Api.Infrastructure.Security;

public interface IKeycloakAdminService
{
    Task AssignRealmRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Keycloak user (or returns the id of an existing one with the same username/email)
    /// and sets the given password. Returns the Keycloak user id.
    /// </summary>
    Task<string> CreateUserAsync(
        string username,
        string email,
        string? firstName,
        string? lastName,
        string password,
        bool temporaryPassword,
        CancellationToken cancellationToken = default);
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

    public async Task<string> CreateUserAsync(
        string username,
        string email,
        string? firstName,
        string? lastName,
        string password,
        bool temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required.", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        var adminToken = await GetAdminAccessTokenAsync(cancellationToken);
        var realm = _configuration["Keycloak:Realm"] ?? "DMRS";
        var adminApiBase = (_configuration["Keycloak:AdminApiBaseUrl"] ?? "http://localhost:8080").TrimEnd('/');
        var usersEndpoint = $"{adminApiBase}/admin/realms/{Uri.EscapeDataString(realm)}/users";

        var userRepresentation = new KeycloakUserRepresentation
        {
            Username = username,
            Email = string.IsNullOrWhiteSpace(email) ? null : email,
            FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName,
            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName,
            Enabled = true,
            EmailVerified = true,
            Credentials =
            [
                new KeycloakCredentialRepresentation
                {
                    Type = "password",
                    Value = password,
                    Temporary = temporaryPassword
                }
            ]
        };

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, usersEndpoint);
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        createRequest.Content = JsonContent.Create(userRepresentation);

        using var createResponse = await _httpClient.SendAsync(createRequest, cancellationToken);

        if (createResponse.IsSuccessStatusCode)
        {
            // Keycloak returns the new user's location in the Location header: .../users/{id}
            var location = createResponse.Headers.Location?.ToString();
            var createdId = ExtractUserIdFromLocation(location);
            if (!string.IsNullOrWhiteSpace(createdId))
            {
                return createdId;
            }

            // Fall through to lookup if the header was missing for any reason.
            return await FindUserIdAsync(adminApiBase, realm, adminToken, username, email, cancellationToken);
        }

        // A 409 means the username/email already exists in Keycloak (e.g. from a previous
        // demo run). Treat that as idempotent and return the existing user's id.
        if (createResponse.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            return await FindUserIdAsync(adminApiBase, realm, adminToken, username, email, cancellationToken);
        }

        var error = await createResponse.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Unable to create Keycloak user '{username}': {createResponse.StatusCode} - {error}");
    }

    private async Task<string> FindUserIdAsync(
        string adminApiBase,
        string realm,
        string adminToken,
        string username,
        string? email,
        CancellationToken cancellationToken)
    {
        var query = !string.IsNullOrWhiteSpace(email)
            ? $"email={Uri.EscapeDataString(email)}&exact=true"
            : $"username={Uri.EscapeDataString(username)}&exact=true";

        using var lookupRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{adminApiBase}/admin/realms/{Uri.EscapeDataString(realm)}/users?{query}");
        lookupRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        using var lookupResponse = await _httpClient.SendAsync(lookupRequest, cancellationToken);
        if (!lookupResponse.IsSuccessStatusCode)
        {
            var error = await lookupResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Unable to look up Keycloak user '{username}': {lookupResponse.StatusCode} - {error}");
        }

        var matches = await lookupResponse.Content.ReadFromJsonAsync<List<KeycloakUserRepresentation>>(cancellationToken: cancellationToken);
        var match = matches?.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u.Id));
        if (match is null || string.IsNullOrWhiteSpace(match.Id))
        {
            throw new InvalidOperationException($"Keycloak user '{username}' could not be resolved after creation.");
        }

        return match.Id;
    }

    private static string? ExtractUserIdFromLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var trimmed = location.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < trimmed.Length - 1
            ? trimmed[(lastSlash + 1)..]
            : null;
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

    private sealed class KeycloakUserRepresentation
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("firstName")]
        public string? FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string? LastName { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("emailVerified")]
        public bool EmailVerified { get; set; }

        [JsonPropertyName("credentials")]
        public List<KeycloakCredentialRepresentation>? Credentials { get; set; }
    }

    private sealed class KeycloakCredentialRepresentation
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "password";

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("temporary")]
        public bool Temporary { get; set; }
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
