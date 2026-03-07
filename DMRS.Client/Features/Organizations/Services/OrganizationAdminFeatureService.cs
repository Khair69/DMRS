using DMRS.Client.Features.Organizations.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace DMRS.Client.Features.Organizations.Services;

public sealed class OrganizationAdminFeatureService
{
    private const string InviteCodeIdentifierSystem = "https://dmrs.local/invites/practitioner";

    private readonly FhirApiService _fhirApiService;
    private readonly string _keycloakAuthority;
    private readonly string _keycloakClientId;

    public OrganizationAdminFeatureService(FhirApiService fhirApiService, IConfiguration configuration)
    {
        _fhirApiService = fhirApiService;
        _keycloakAuthority = configuration["Keycloak:Authority"] ?? "http://localhost:8080/realms/DMRS";
        _keycloakClientId = configuration["Keycloak:ClientId"] ?? "dmrs-api";
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
            var autoClaimLink = $"{claimLink}&auto=true";
            var registrationLink = BuildKeycloakRegistrationUrl(autoClaimLink);

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
                // Best-effort cleanup only.
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

        var response = await _fhirApiService.PostApiJsonAsync<StaffClaimRequest, StaffClaimApiResponse>("api/staff/claim-invite",
            new StaffClaimRequest
            {
                InviteCode = inviteCode,
                KeycloakUserId = keycloakUserId,
                KeycloakUsername = keycloakUsername
            });

        if (response is null || string.IsNullOrWhiteSpace(response.PractitionerId))
        {
            throw new InvalidOperationException("Claim failed: empty response from API.");
        }

        return new OrganizationAdminClaimResult(response.PractitionerId);
    }

    private string BuildKeycloakRegistrationUrl(string redirectUri)
    {
        var authBase = _keycloakAuthority.TrimEnd('/');
        return $"{authBase}/protocol/openid-connect/registrations" +
               $"?client_id={Uri.EscapeDataString(_keycloakClientId)}" +
               $"&response_type=code" +
               $"&scope={Uri.EscapeDataString("openid profile")}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";
    }

    private static string GenerateInviteCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToHexString(bytes);
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

internal sealed class StaffClaimRequest
{
    public string InviteCode { get; set; } = string.Empty;
    public string KeycloakUserId { get; set; } = string.Empty;
    public string? KeycloakUsername { get; set; }
}

internal sealed class StaffClaimApiResponse
{
    public string PractitionerId { get; set; } = string.Empty;
    public string AssignedRealmRole { get; set; } = string.Empty;
}
