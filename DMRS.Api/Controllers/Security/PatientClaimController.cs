using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Security;

[ApiController]
[Route("api/patients")]
[Authorize]
public sealed class PatientClaimController : ControllerBase
{
    private const string InviteCodeIdentifierSystem = "https://dmrs.local/invites/patient";
    private const string PatientRealmRole = "ROLE_PATIENT";
    private const string KeycloakIdentifierSystemFallback = "https://keycloak.local/users";

    private readonly IFhirRepository _repository;
    private readonly PatientIndexer _patientIndexer;
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IConfiguration _configuration;

    public PatientClaimController(
        IFhirRepository repository,
        PatientIndexer patientIndexer,
        IKeycloakAdminService keycloakAdminService,
        IConfiguration configuration)
    {
        _repository = repository;
        _patientIndexer = patientIndexer;
        _keycloakAdminService = keycloakAdminService;
        _configuration = configuration;
    }

    [HttpPost("claim-invite")]
    public async Task<IActionResult> ClaimInvite([FromBody] PatientClaimRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.InviteCode))
        {
            return BadRequest("Invite code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.KeycloakUserId))
        {
            return BadRequest("Keycloak user id is required.");
        }

        var patientMatches = await _repository.SearchAsync<Patient>(new Dictionary<string, string>
        {
            ["identifier"] = $"{InviteCodeIdentifierSystem}|{request.InviteCode.Trim()}"
        });

        if (patientMatches.Count == 0)
        {
            return BadRequest("Invite code is invalid or already claimed.");
        }

        if (patientMatches.Count > 1)
        {
            return Conflict("Invite code is not unique.");
        }

        var patient = patientMatches[0];
        if (string.IsNullOrWhiteSpace(patient.Id))
        {
            return StatusCode(500, "Matched patient has no id.");
        }

        var keycloakIdentifierSystem = BuildKeycloakIdentifierSystem(_configuration["Keycloak:Authority"]);
        var existingLinked = await _repository.SearchAsync<Patient>(new Dictionary<string, string>
        {
            ["identifier"] = $"{keycloakIdentifierSystem}|{request.KeycloakUserId.Trim()}"
        });

        var duplicateLink = existingLinked.Any(p => !string.Equals(p.Id, patient.Id, StringComparison.OrdinalIgnoreCase));
        if (duplicateLink)
        {
            return Conflict("This Keycloak account is already linked to another patient.");
        }

        await _keycloakAdminService.AssignRealmRoleAsync(request.KeycloakUserId.Trim(), PatientRealmRole, cancellationToken);

        patient.Identifier ??= [];
        patient.Identifier = patient.Identifier
            .Where(i => !string.Equals(i.System, InviteCodeIdentifierSystem, StringComparison.OrdinalIgnoreCase))
            .ToList();

        AddOrReplaceIdentifier(patient.Identifier, keycloakIdentifierSystem, request.KeycloakUserId.Trim());

        if (!string.IsNullOrWhiteSpace(request.KeycloakUsername))
        {
            AddOrReplaceIdentifier(patient.Identifier, $"{keycloakIdentifierSystem}/username", request.KeycloakUsername.Trim());
        }

        await _repository.UpdateAsync(patient.Id, patient, _patientIndexer);

        return Ok(new PatientClaimResponse
        {
            PatientId = patient.Id,
            AssignedRealmRole = PatientRealmRole
        });
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

    public sealed class PatientClaimRequest
    {
        public string InviteCode { get; set; } = string.Empty;
        public string KeycloakUserId { get; set; } = string.Empty;
        public string? KeycloakUsername { get; set; }
    }

    public sealed class PatientClaimResponse
    {
        public string PatientId { get; set; } = string.Empty;
        public string AssignedRealmRole { get; set; } = string.Empty;
    }
}
