using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Security;

[ApiController]
[Route("api/staff")]
[Authorize]
public sealed class StaffClaimController : ControllerBase
{
    private const string InviteCodeIdentifierSystem = "https://dmrs.local/invites/practitioner";
    private const string OrganizationAdminRoleCode = "ORG_ADMIN";
    private const string DoctorRoleCode = "DOCTOR";
    private const string KeycloakIdentifierSystemFallback = "https://keycloak.local/users";

    private readonly IFhirRepository _repository;
    private readonly PractitionerIndexer _practitionerIndexer;
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IConfiguration _configuration;

    public StaffClaimController(
        IFhirRepository repository,
        PractitionerIndexer practitionerIndexer,
        IKeycloakAdminService keycloakAdminService,
        IConfiguration configuration)
    {
        _repository = repository;
        _practitionerIndexer = practitionerIndexer;
        _keycloakAdminService = keycloakAdminService;
        _configuration = configuration;
    }

    [HttpPost("claim-invite")]
    public async Task<IActionResult> ClaimInvite([FromBody] StaffClaimRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.InviteCode))
        {
            return BadRequest("Invite code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.KeycloakUserId))
        {
            return BadRequest("Keycloak user id is required.");
        }

        var practitionerMatches = await _repository.SearchAsync<Practitioner>(new Dictionary<string, string>
        {
            ["identifier"] = $"{InviteCodeIdentifierSystem}|{request.InviteCode.Trim()}"
        });

        if (practitionerMatches.Count == 0)
        {
            return BadRequest("Invite code is invalid or already claimed.");
        }

        if (practitionerMatches.Count > 1)
        {
            return Conflict("Invite code is not unique.");
        }

        var practitioner = practitionerMatches[0];
        if (string.IsNullOrWhiteSpace(practitioner.Id))
        {
            return StatusCode(500, "Matched practitioner has no id.");
        }

        // Prevent linking one Keycloak account to multiple practitioners.
        var keycloakIdentifierSystem = BuildKeycloakIdentifierSystem(_configuration["Keycloak:Authority"]);
        var existingLinked = await _repository.SearchAsync<Practitioner>(new Dictionary<string, string>
        {
            ["identifier"] = $"{keycloakIdentifierSystem}|{request.KeycloakUserId.Trim()}"
        });

        var duplicateLink = existingLinked.Any(p => !string.Equals(p.Id, practitioner.Id, StringComparison.OrdinalIgnoreCase));
        if (duplicateLink)
        {
            return Conflict("This Keycloak account is already linked to another practitioner.");
        }

        var practitionerRoles = await _repository.SearchAsync<PractitionerRole>(new Dictionary<string, string>
        {
            ["practitioner"] = $"Practitioner/{practitioner.Id}"
        });

        var realmRoleToAssign = ResolveRealmRole(practitionerRoles);
        await _keycloakAdminService.AssignRealmRoleAsync(request.KeycloakUserId.Trim(), realmRoleToAssign, cancellationToken);

        practitioner.Identifier ??= [];
        practitioner.Identifier = practitioner.Identifier
            .Where(i => !string.Equals(i.System, InviteCodeIdentifierSystem, StringComparison.OrdinalIgnoreCase))
            .ToList();

        AddOrReplaceIdentifier(practitioner.Identifier, keycloakIdentifierSystem, request.KeycloakUserId.Trim());

        if (!string.IsNullOrWhiteSpace(request.KeycloakUsername))
        {
            AddOrReplaceIdentifier(practitioner.Identifier, $"{keycloakIdentifierSystem}/username", request.KeycloakUsername.Trim());
        }

        await _repository.UpdateAsync(practitioner.Id, practitioner, _practitionerIndexer);

        return Ok(new StaffClaimResponse
        {
            PractitionerId = practitioner.Id,
            AssignedRealmRole = realmRoleToAssign
        });
    }

    private static string ResolveRealmRole(IReadOnlyList<PractitionerRole> roles)
    {
        var roleCodes = roles
            .SelectMany(r => r.Code)
            .SelectMany(c => c.Coding)
            .Select(c => c.Code)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim().ToUpperInvariant())
            .ToHashSet();

        if (roleCodes.Contains(OrganizationAdminRoleCode))
        {
            return "ROLE_ORG_ADMIN";
        }

        if (roleCodes.Contains(DoctorRoleCode))
        {
            return "ROLE_PRACTITIONER";
        }

        return "ROLE_PRACTITIONER";
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

    private static string BuildKeycloakIdentifierSystem(string? keycloakAuthority)
    {
        if (string.IsNullOrWhiteSpace(keycloakAuthority))
        {
            return KeycloakIdentifierSystemFallback;
        }

        return $"{keycloakAuthority.TrimEnd('/')}/users";
    }

    public sealed class StaffClaimRequest
    {
        public string InviteCode { get; set; } = string.Empty;
        public string KeycloakUserId { get; set; } = string.Empty;
        public string? KeycloakUsername { get; set; }
    }

    public sealed class StaffClaimResponse
    {
        public string PractitionerId { get; set; } = string.Empty;
        public string AssignedRealmRole { get; set; } = string.Empty;
    }
}
