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
        var roles = await _fhirApiService.SearchAsync<PractitionerRole>("organization", $"Organization/{organizationId}");
        var summaries = new List<StaffSummaryViewModel>();

        foreach (var role in roles)
        {
            if (!MatchesRoleFilter(role, roleCodeFilter))
            {
                continue;
            }

            var practitionerId = ParseReferenceId(role.Practitioner?.Reference, "Practitioner");
            if (string.IsNullOrWhiteSpace(practitionerId))
            {
                continue;
            }

            var practitioner = await _fhirApiService.GetResourceAsync<Practitioner>(practitionerId);
            if (practitioner is null || string.IsNullOrWhiteSpace(practitioner.Id))
            {
                continue;
            }

            var summary = MapSummary(practitioner, role);
            if (!MatchesTextFilter(summary, textFilter))
            {
                continue;
            }

            summaries.Add(summary);
        }

        return summaries
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
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

    private static bool MatchesRoleFilter(PractitionerRole role, string? roleCodeFilter)
    {
        if (string.IsNullOrWhiteSpace(roleCodeFilter) || roleCodeFilter == "ALL")
        {
            return true;
        }

        return role.Code.SelectMany(c => c.Coding)
            .Any(c => string.Equals(c.Code, roleCodeFilter, StringComparison.OrdinalIgnoreCase));
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

    private static StaffSummaryViewModel MapSummary(Practitioner practitioner, PractitionerRole role)
    {
        var name = practitioner.Name.FirstOrDefault();
        var displayName = string.Join(" ", new[] { name?.Given?.FirstOrDefault(), name?.Family }.Where(v => !string.IsNullOrWhiteSpace(v)));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = practitioner.Id ?? "(no-name)";
        }

        var email = practitioner.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email)?.Value ?? string.Empty;
        var phone = practitioner.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone)?.Value;

        var coding = role.Code.SelectMany(c => c.Coding).FirstOrDefault();
        var roleCode = coding?.Code ?? "UNKNOWN";
        var roleDisplay = coding?.Display ?? role.Code.FirstOrDefault()?.Text ?? roleCode;

        return new StaffSummaryViewModel(
            practitioner.Id ?? "(no-id)",
            role.Id ?? "(no-role-id)",
            displayName,
            email,
            phone,
            practitioner.Active ?? false,
            roleCode,
            roleDisplay);
    }

    private static string? ParseReferenceId(string? reference, string expectedType)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var prefix = $"{expectedType}/";
        if (!reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return reference[prefix.Length..];
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
