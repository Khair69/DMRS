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

        var inviteParams = new Dictionary<string, string>
        {
            ["identifier"] = $"{StaffAccountLinking.InviteCodeIdentifierSystem}|{request.InviteCode.Trim()}"
        };

        var practitionerMatches = await _repository.SearchAsync<Practitioner>(inviteParams);
        var practitionerRawCount = await _repository.SearchCountAsync<Practitioner>(inviteParams);

        // If raw count exceeds deserialized count, one or more records were silently skipped
        // due to JSON corruption. Fail with a 500 rather than silently treating a valid invite
        // as missing (which would lock out the practitioner).
        if (practitionerRawCount != practitionerMatches.Count)
        {
            return StatusCode(500, "Invite code lookup encountered a data integrity error. Please contact support.");
        }

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
        var keycloakIdentifierSystem = StaffAccountLinking.BuildKeycloakIdentifierSystem(_configuration["Keycloak:Authority"]);
        var linkedParams = new Dictionary<string, string>
        {
            ["identifier"] = $"{keycloakIdentifierSystem}|{request.KeycloakUserId.Trim()}"
        };

        var existingLinked = await _repository.SearchAsync<Practitioner>(linkedParams);
        var existingLinkedRawCount = await _repository.SearchCountAsync<Practitioner>(linkedParams);

        // Fail closed: if any linked record couldn't be deserialized, block the link to prevent
        // a malformed record from bypassing the one-account-per-practitioner invariant.
        if (existingLinkedRawCount != existingLinked.Count)
        {
            return Conflict("This Keycloak account is already linked to another practitioner.");
        }

        var duplicateLink = existingLinked.Any(p => !string.Equals(p.Id, practitioner.Id, StringComparison.OrdinalIgnoreCase));
        if (duplicateLink)
        {
            return Conflict("This Keycloak account is already linked to another practitioner.");
        }

        var practitionerRoles = await _repository.SearchAsync<PractitionerRole>(new Dictionary<string, string>
        {
            ["practitioner"] = $"Practitioner/{practitioner.Id}"
        });

        var realmRoleToAssign = StaffAccountLinking.ResolveRealmRole(practitionerRoles);
        await _keycloakAdminService.AssignRealmRoleAsync(request.KeycloakUserId.Trim(), realmRoleToAssign, cancellationToken);

        practitioner.Identifier ??= [];
        practitioner.Identifier = practitioner.Identifier
            .Where(i => !string.Equals(i.System, StaffAccountLinking.InviteCodeIdentifierSystem, StringComparison.OrdinalIgnoreCase))
            .ToList();

        StaffAccountLinking.AddOrReplaceIdentifier(practitioner.Identifier, keycloakIdentifierSystem, request.KeycloakUserId.Trim());

        if (!string.IsNullOrWhiteSpace(request.KeycloakUsername))
        {
            StaffAccountLinking.AddOrReplaceIdentifier(practitioner.Identifier, $"{keycloakIdentifierSystem}/username", request.KeycloakUsername.Trim());
        }

        await _repository.UpdateAsync(practitioner.Id, practitioner, _practitionerIndexer);

        return Ok(new StaffClaimResponse
        {
            PractitionerId = practitioner.Id,
            AssignedRealmRole = realmRoleToAssign
        });
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
