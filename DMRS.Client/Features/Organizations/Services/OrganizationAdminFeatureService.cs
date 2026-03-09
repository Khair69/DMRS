using DMRS.Client.Features.Organizations.Models;
using DMRS.Client.Services;

namespace DMRS.Client.Features.Organizations.Services;

public sealed class OrganizationAdminFeatureService
{
    private readonly FhirApiService _fhirApiService;

    public OrganizationAdminFeatureService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
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

        var response = await _fhirApiService.PostApiJsonAsync<CreateStaffInviteRequest, CreateStaffInviteResponse>(
            "api/staff/create-invite",
            new CreateStaffInviteRequest
            {
                OrganizationId = organizationId,
                AppBaseUri = appBaseUri,
                ClaimPath = "/org-admin/claim",
                GivenName = model.GivenName,
                FamilyName = model.FamilyName,
                Email = model.Email,
                Phone = model.Phone,
                IdentifierSystem = model.IdentifierSystem,
                IdentifierValue = model.IdentifierValue,
                RoleSystem = model.RoleSystem,
                RoleCode = model.RoleCode,
                RoleDisplay = model.RoleDisplay
            });

        if (response is null || string.IsNullOrWhiteSpace(response.PractitionerId) || string.IsNullOrWhiteSpace(response.PractitionerRoleId))
        {
            throw new InvalidOperationException("Invite creation failed: empty response from API.");
        }

        return new OrganizationAdminInviteResult(
            response.PractitionerId,
            response.PractitionerRoleId,
            response.InviteCode,
            response.ClaimLink,
            response.RegistrationLink);
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

internal sealed class CreateStaffInviteRequest
{
    public string OrganizationId { get; set; } = string.Empty;
    public string AppBaseUri { get; set; } = string.Empty;
    public string? ClaimPath { get; set; }
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? IdentifierSystem { get; set; }
    public string? IdentifierValue { get; set; }
    public string? RoleSystem { get; set; }
    public string? RoleCode { get; set; }
    public string? RoleDisplay { get; set; }
}

internal sealed class CreateStaffInviteResponse
{
    public string PractitionerId { get; set; } = string.Empty;
    public string PractitionerRoleId { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public string ClaimLink { get; set; } = string.Empty;
    public string RegistrationLink { get; set; } = string.Empty;
}
