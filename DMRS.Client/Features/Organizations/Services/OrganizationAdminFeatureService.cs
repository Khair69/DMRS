using DMRS.Client.Features.Organizations.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace DMRS.Client.Features.Organizations.Services;

public sealed class OrganizationAdminFeatureService
{
    private const string InviteCodeIdentifierSystem = "https://dmrs.local/invites/org-admin";

    private readonly FhirApiService _fhirApiService;
    private readonly string _keycloakIdentifierSystem;
    private readonly string _keycloakAuthority;
    private readonly string _keycloakClientId;
    private readonly string _keycloakRedirectUri;

    public OrganizationAdminFeatureService(FhirApiService fhirApiService, IConfiguration configuration)
    {
        _fhirApiService = fhirApiService;
        _keycloakAuthority = configuration["Keycloak:Authority"] ?? "http://localhost:8080/realms/DMRS";
        _keycloakClientId = configuration["Keycloak:ClientId"] ?? "dmrs-api";
        _keycloakRedirectUri = configuration["Keycloak:RedirectUri"] ?? "https://localhost:7099/authentication/login-callback";
        _keycloakIdentifierSystem = BuildKeycloakIdentifierSystem(_keycloakAuthority);
    }

    public async Task<OrganizationAdminInviteResult> CreateAdminInviteAsync(string organizationId, OrganizationAdminEditModel model, string appBaseUri)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            throw new ArgumentException("Organization id is required.", nameof(organizationId));
        }

        if (string.IsNullOrWhiteSpace(appBaseUri))
        {
            throw new ArgumentException("Application base URI is required.", nameof(appBaseUri));
        }

        var practitioner = model.ToPractitioner();
        var createdPractitioner = await _fhirApiService.CreateResourceAsync(practitioner);

        if (createdPractitioner?.Id is null)
        {
            throw new InvalidOperationException("Practitioner was created but no id was returned.");
        }

        try
        {
            var role = model.ToPractitionerRole(createdPractitioner.Id, organizationId);
            var createdRole = await _fhirApiService.CreateResourceAsync(role);

            if (createdRole?.Id is null)
            {
                throw new InvalidOperationException("PractitionerRole was created but no id was returned.");
            }

            var inviteCode = GenerateInviteCode();
            createdPractitioner.Identifier ??= [];
            createdPractitioner.Identifier.Add(new Identifier
            {
                System = InviteCodeIdentifierSystem,
                Value = inviteCode
            });

            await _fhirApiService.UpdateResourceAsync<Practitioner>(createdPractitioner.Id, createdPractitioner);

            var claimLink = $"{appBaseUri.TrimEnd('/')}/org-admin/claim?code={Uri.EscapeDataString(inviteCode)}";
            var registrationLink = BuildKeycloakRegistrationUrl(claimLink);

            return new OrganizationAdminInviteResult(
                createdPractitioner.Id,
                createdRole.Id,
                inviteCode,
                claimLink,
                registrationLink);
        }
        catch
        {
            try
            {
                await _fhirApiService.DeleteResourceAsync<Practitioner>(createdPractitioner.Id);
            }
            catch
            {
                Console.WriteLine("error kteer... dbr rask");
            }

            throw;
        }
    }

    public async Task<OrganizationAdminClaimResult> ClaimInviteAsync(string inviteCode, string keycloakUserId, string? keycloakUsername)
    {
        if (string.IsNullOrWhiteSpace(inviteCode))
        {
            throw new ArgumentException("Invite code is required.", nameof(inviteCode));
        }

        if (string.IsNullOrWhiteSpace(keycloakUserId))
        {
            throw new InvalidOperationException("Current user id claim (sub) is missing.");
        }

        var matches = await _fhirApiService.SearchAsync<Practitioner>("identifier", $"{InviteCodeIdentifierSystem}|{inviteCode}");
        if (matches.Count == 0)
        {
            throw new InvalidOperationException("Invite code is invalid or already claimed.");
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException("Invite code is not unique. Contact support.");
        }

        var practitioner = matches[0];
        if (string.IsNullOrWhiteSpace(practitioner.Id))
        {
            throw new InvalidOperationException("Matched practitioner has no id.");
        }

        practitioner.Identifier ??= [];

        practitioner.Identifier = practitioner.Identifier
            .Where(i => !string.Equals(i.System, InviteCodeIdentifierSystem, StringComparison.OrdinalIgnoreCase))
            .ToList();

        AddOrReplaceIdentifier(practitioner.Identifier, _keycloakIdentifierSystem, keycloakUserId);

        if (!string.IsNullOrWhiteSpace(keycloakUsername))
        {
            AddOrReplaceIdentifier(practitioner.Identifier, $"{_keycloakIdentifierSystem}/username", keycloakUsername);
        }

        await _fhirApiService.UpdateResourceAsync<Practitioner>(practitioner.Id, practitioner);
        return new OrganizationAdminClaimResult(practitioner.Id);
    }

    private static void AddOrReplaceIdentifier(List<Identifier> identifiers, string system, string value)
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

    private string BuildKeycloakRegistrationUrl(string _)
    {
        var authBase = _keycloakAuthority.TrimEnd('/');
        return $"{authBase}/protocol/openid-connect/registrations" +
               $"?client_id={Uri.EscapeDataString(_keycloakClientId)}" +
               $"&response_type=code" +
                $"&scope={Uri.EscapeDataString("openid profile")}" +
               $"&redirect_uri={Uri.EscapeDataString(_keycloakRedirectUri)}";
    }

    private static string GenerateInviteCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToHexString(bytes);
    }

    private static string BuildKeycloakIdentifierSystem(string? keycloakAuthority)
    {
        if (string.IsNullOrWhiteSpace(keycloakAuthority))
        {
            return "https://keycloak.local/users";
        }

        return $"{keycloakAuthority.TrimEnd('/')}/users";
    }
}

public sealed record OrganizationAdminInviteResult(
    string PractitionerId,
    string PractitionerRoleId,
    string InviteCode,
    string ClaimLink,
    string RegistrationLink);

public sealed record OrganizationAdminClaimResult(
    string PractitionerId);
