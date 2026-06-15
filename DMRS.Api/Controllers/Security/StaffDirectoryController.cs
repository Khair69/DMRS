using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Security;

/// <summary>
/// Read-only staff directory for a single organization. Returns each practitioner's summary
/// (including login-account status) in ONE response, so the client doesn't have to make one
/// HTTP call per practitioner — that N+1 was throttled by the browser's per-host connection cap
/// and dominated the org-admin dashboard load time.
/// </summary>
[ApiController]
[Route("api/staff")]
[Authorize]
public sealed class StaffDirectoryController : ControllerBase
{
    private readonly IFhirRepository _repository;
    private readonly ISmartAuthorizationService _authorizationService;

    public StaffDirectoryController(
        IFhirRepository repository,
        ISmartAuthorizationService authorizationService)
    {
        _repository = repository;
        _authorizationService = authorizationService;
    }

    [HttpGet("by-organization/{organizationId}")]
    public async Task<ActionResult<IReadOnlyList<StaffSummaryDto>>> GetByOrganization(string organizationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return BadRequest("Organization id is required.");
        }

        var accessLevel = _authorizationService.GetAccessLevel(User, "PractitionerRole", "read");
        if (accessLevel is SmartAccessLevel.None or SmartAccessLevel.Patient)
        {
            return Forbid();
        }

        if (accessLevel == SmartAccessLevel.User)
        {
            var callerOrganizations = await _authorizationService.ResolveOrganizationIdsAsync(User);
            if (!callerOrganizations.Contains(organizationId, StringComparer.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        var roles = await _repository.SearchAsync<PractitionerRole>(new Dictionary<string, string>
        {
            ["organization"] = $"Organization/{organizationId}"
        });

        var summaries = new List<StaffSummaryDto>(roles.Count);
        // Practitioner reads here are local DB queries (no per-host HTTP cap), so a loop is fine and
        // still collapses to a single response for the client.
        foreach (var role in roles)
        {
            var practitionerId = ExtractReferenceId(role.Practitioner?.Reference, "Practitioner");
            if (string.IsNullOrWhiteSpace(practitionerId))
            {
                continue;
            }

            var practitioner = await _repository.GetAsync<Practitioner>(practitionerId);
            if (practitioner is null || string.IsNullOrWhiteSpace(practitioner.Id))
            {
                continue;
            }

            summaries.Add(MapSummary(practitioner, role));
        }

        return Ok(summaries
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList());
    }

    private static StaffSummaryDto MapSummary(Practitioner practitioner, PractitionerRole role)
    {
        var name = practitioner.Name.FirstOrDefault();
        var displayName = string.Join(" ", new[] { name?.Given?.FirstOrDefault(), name?.Family }
            .Where(v => !string.IsNullOrWhiteSpace(v)));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = practitioner.Id ?? "(no-name)";
        }

        var email = practitioner.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email)?.Value ?? string.Empty;
        var phone = practitioner.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone)?.Value;

        var coding = role.Code.SelectMany(c => c.Coding).FirstOrDefault();
        var roleCode = coding?.Code ?? "UNKNOWN";
        var roleDisplay = coding?.Display ?? role.Code.FirstOrDefault()?.Text ?? roleCode;

        var specialtyCoding = role.Specialty.SelectMany(s => s.Coding).FirstOrDefault();
        var specialty = specialtyCoding?.Display ?? role.Specialty.FirstOrDefault()?.Text;

        // A linked Keycloak account is recorded as an identifier whose system ends with "/users".
        var hasLoginAccount = practitioner.Identifier?.Any(i =>
            !string.IsNullOrWhiteSpace(i.System)
            && i.System.EndsWith("/users", StringComparison.OrdinalIgnoreCase)) == true;

        return new StaffSummaryDto
        {
            PractitionerId = practitioner.Id ?? "(no-id)",
            PractitionerRoleId = role.Id ?? "(no-role-id)",
            DisplayName = displayName,
            Email = email,
            Phone = phone,
            Active = practitioner.Active ?? false,
            RoleCode = roleCode,
            RoleDisplay = roleDisplay,
            Specialty = specialty,
            HasLoginAccount = hasLoginAccount
        };
    }

    private static string? ExtractReferenceId(string? reference, string expectedType)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var prefix = $"{expectedType}/";
        return reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? reference[prefix.Length..]
            : null;
    }

    public sealed class StaffSummaryDto
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
}
