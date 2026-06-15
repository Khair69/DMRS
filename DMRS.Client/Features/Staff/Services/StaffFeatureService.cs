using DMRS.Client.Features.Staff.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Staff.Services;

public sealed class StaffFeatureService
{
    private readonly FhirApiService _fhirApiService;

    public StaffFeatureService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    public async Task<IReadOnlyList<StaffSummaryViewModel>> GetByOrganizationAsync(string organizationId, string? roleCodeFilter, string? textFilter)
    {
        // One request to the server, which resolves PractitionerRoles + Practitioners DB-side and
        // returns the whole roster. This replaced an N+1 (a Practitioner GET per role) that was
        // throttled by the browser's per-host connection cap.
        var dtos = await _fhirApiService.GetApiJsonAsync<List<StaffSummaryApiModel>>(
            $"api/staff/by-organization/{Uri.EscapeDataString(organizationId)}") ?? [];

        var summaries = dtos
            .Select(d => new StaffSummaryViewModel(
                d.PractitionerId,
                d.PractitionerRoleId,
                d.DisplayName,
                d.Email,
                d.Phone,
                d.Active,
                d.RoleCode,
                d.RoleDisplay,
                d.Specialty,
                d.HasLoginAccount))
            .Where(summary => MatchesSummaryRoleFilter(summary, roleCodeFilter) && MatchesTextFilter(summary, textFilter))
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return summaries;
    }

    public Task<Practitioner?> GetPractitionerAsync(string practitionerId)
    {
        return _fhirApiService.GetResourceAsync<Practitioner>(practitionerId);
    }

    public Task<IReadOnlyList<PractitionerRole>> GetPractitionerRolesAsync(string practitionerId)
    {
        return _fhirApiService.SearchAsync<PractitionerRole>("practitioner", $"Practitioner/{practitionerId}");
    }

    public async Task<StaffInviteResult> CreateInviteAsync(string organizationId, StaffInviteEditModel model, string appBaseUri)
    {
        var response = await _fhirApiService.PostApiJsonAsync<CreateStaffInviteRequest, CreateStaffInviteResponse>(
            "api/staff/create-invite",
            new CreateStaffInviteRequest
            {
                OrganizationId = organizationId,
                AppBaseUri = appBaseUri,
                ClaimPath = "/staff/claim",
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

        return new StaffInviteResult(
            response.PractitionerId,
            response.PractitionerRoleId,
            response.InviteCode,
            response.ClaimLink,
            response.RegistrationLink);
    }

    public async Task<StaffProvisionResult> ProvisionAccountAsync(string organizationId, string practitionerId)
    {
        var response = await _fhirApiService.PostApiJsonAsync<ProvisionAccountRequest, ProvisionAccountResponse>(
            "api/staff/provision-account",
            new ProvisionAccountRequest
            {
                OrganizationId = organizationId,
                PractitionerId = practitionerId
            });

        if (response is null || string.IsNullOrWhiteSpace(response.KeycloakUserId))
        {
            throw new InvalidOperationException("Account provisioning failed: empty response from API.");
        }

        return new StaffProvisionResult(
            response.PractitionerId,
            response.KeycloakUserId,
            response.Username,
            response.Password,
            response.AssignedRealmRole);
    }

    public async Task<StaffClaimResult> ClaimInviteAsync(string inviteCode, string keycloakUserId, string? keycloakUsername)
    {
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

        return new StaffClaimResult(response.PractitionerId);
    }

    private static bool MatchesSummaryRoleFilter(StaffSummaryViewModel summary, string? roleCodeFilter)
    {
        if (string.IsNullOrWhiteSpace(roleCodeFilter) || roleCodeFilter == "ALL")
        {
            return true;
        }

        return string.Equals(summary.RoleCode, roleCodeFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesTextFilter(StaffSummaryViewModel summary, string? textFilter)
    {
        if (string.IsNullOrWhiteSpace(textFilter))
        {
            return true;
        }

        var text = textFilter.Trim();
        return summary.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase)
               || summary.Email.Contains(text, StringComparison.OrdinalIgnoreCase)
               || summary.PractitionerId.Contains(text, StringComparison.OrdinalIgnoreCase)
               || summary.RoleDisplay.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

}

public sealed record StaffInviteResult(
    string PractitionerId,
    string PractitionerRoleId,
    string InviteCode,
    string ClaimLink,
    string RegistrationLink);

public sealed record StaffClaimResult(
    string PractitionerId);

public sealed record StaffProvisionResult(
    string PractitionerId,
    string KeycloakUserId,
    string Username,
    string Password,
    string AssignedRealmRole);

internal sealed class StaffSummaryApiModel
{
    public string PractitionerId { get; set; } = string.Empty;
    public string PractitionerRoleId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public bool Active { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public string RoleDisplay { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public bool HasLoginAccount { get; set; }
}

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

internal sealed class ProvisionAccountRequest
{
    public string PractitionerId { get; set; } = string.Empty;
    public string OrganizationId { get; set; } = string.Empty;
}

internal sealed class ProvisionAccountResponse
{
    public string PractitionerId { get; set; } = string.Empty;
    public string KeycloakUserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AssignedRealmRole { get; set; } = string.Empty;
}
