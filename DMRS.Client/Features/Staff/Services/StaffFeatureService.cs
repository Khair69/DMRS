using System.Security.Cryptography;
using DMRS.Client.Features.Staff.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Configuration;

namespace DMRS.Client.Features.Staff.Services;

public sealed class StaffFeatureService
{
    private const string InviteCodeIdentifierSystem = "https://dmrs.local/invites/practitioner";

    private readonly FhirApiService _fhirApiService;
    private readonly string _keycloakAuthority;
    private readonly string _keycloakClientId;

    public StaffFeatureService(FhirApiService fhirApiService, IConfiguration configuration)
    {
        _fhirApiService = fhirApiService;
        _keycloakAuthority = configuration["Keycloak:Authority"] ?? "http://localhost:8080/realms/DMRS";
        _keycloakClientId = configuration["Keycloak:ClientId"] ?? "dmrs-api";
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

            var claimLink = $"{appBaseUri.TrimEnd('/')}/staff/claim?code={Uri.EscapeDataString(inviteCode)}";
            var autoClaimLink = $"{claimLink}&auto=true";
            var registrationLink = BuildKeycloakRegistrationUrl(autoClaimLink);

            return new StaffInviteResult(
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
                Console.WriteLine("BIG ERROR ig");
            }

            throw;
        }
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
