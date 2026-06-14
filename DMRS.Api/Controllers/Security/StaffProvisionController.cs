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
public sealed class StaffProvisionController : ControllerBase
{
    private const string DefaultDemoPassword = "Demo123!";

    private readonly IFhirRepository _repository;
    private readonly PractitionerIndexer _practitionerIndexer;
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly ISmartAuthorizationService _authorizationService;
    private readonly IConfiguration _configuration;

    public StaffProvisionController(
        IFhirRepository repository,
        PractitionerIndexer practitionerIndexer,
        IKeycloakAdminService keycloakAdminService,
        ISmartAuthorizationService authorizationService,
        IConfiguration configuration)
    {
        _repository = repository;
        _practitionerIndexer = practitionerIndexer;
        _keycloakAdminService = keycloakAdminService;
        _authorizationService = authorizationService;
        _configuration = configuration;
    }

    /// <summary>
    /// Demo helper: directly provisions a Keycloak login account for an existing practitioner
    /// (doctor or org admin) that does not yet have one, then links it to the resource exactly
    /// as the invite-claim flow would. Returns the credentials so the account can be used to log in.
    /// </summary>
    [HttpPost("provision-account")]
    public async Task<IActionResult> ProvisionAccount([FromBody] ProvisionAccountRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.PractitionerId))
        {
            return BadRequest("Practitioner id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OrganizationId))
        {
            return BadRequest("Organization id is required.");
        }

        var practitionerAccess = _authorizationService.GetAccessLevel(User, "Practitioner", "write");
        var roleAccess = _authorizationService.GetAccessLevel(User, "PractitionerRole", "write");
        if (practitionerAccess == SmartAccessLevel.None || roleAccess == SmartAccessLevel.None)
        {
            return Forbid();
        }

        if (practitionerAccess == SmartAccessLevel.Patient || roleAccess == SmartAccessLevel.Patient)
        {
            return Forbid();
        }

        var accessLevel = practitionerAccess == SmartAccessLevel.System || roleAccess == SmartAccessLevel.System
            ? SmartAccessLevel.System
            : SmartAccessLevel.User;

        var organizationId = request.OrganizationId.Trim();
        var practitionerId = request.PractitionerId.Trim();

        if (accessLevel == SmartAccessLevel.User)
        {
            var callerOrganizations = await _authorizationService.ResolveOrganizationIdsAsync(User);
            if (!callerOrganizations.Contains(organizationId, StringComparer.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        var practitioner = await _repository.GetAsync<Practitioner>(practitionerId);
        if (practitioner is null || string.IsNullOrWhiteSpace(practitioner.Id))
        {
            return NotFound($"Practitioner/{practitionerId} was not found.");
        }

        var practitionerRoles = await _repository.SearchAsync<PractitionerRole>(new Dictionary<string, string>
        {
            ["practitioner"] = $"Practitioner/{practitioner.Id}"
        });

        // The practitioner must actually belong to the supplied organization. This both enforces
        // org-admin scoping and guards against provisioning an unrelated practitioner.
        var belongsToOrg = practitionerRoles.Any(r =>
            string.Equals(r.Organization?.Reference, $"Organization/{organizationId}", StringComparison.OrdinalIgnoreCase));
        if (!belongsToOrg)
        {
            return Forbid();
        }

        var keycloakAuthority = _configuration["Keycloak:Authority"];
        var existingUserId = StaffAccountLinking.GetLinkedKeycloakUserId(practitioner, keycloakAuthority);
        if (!string.IsNullOrWhiteSpace(existingUserId))
        {
            return Conflict("This practitioner already has a linked login account.");
        }

        var email = practitioner.Telecom?
            .FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email)?
            .Value?
            .Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("This practitioner has no email address, which is required to create a login account.");
        }

        var name = practitioner.Name?.FirstOrDefault();
        var givenName = name?.Given?.FirstOrDefault();
        var familyName = name?.Family;

        var password = _configuration["Keycloak:DemoUserPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            password = DefaultDemoPassword;
        }

        var keycloakUserId = await _keycloakAdminService.CreateUserAsync(
            username: email,
            email: email,
            firstName: givenName,
            lastName: familyName,
            password: password,
            temporaryPassword: false,
            cancellationToken);

        var realmRoleToAssign = StaffAccountLinking.ResolveRealmRole(practitionerRoles);
        await _keycloakAdminService.AssignRealmRoleAsync(keycloakUserId, realmRoleToAssign, cancellationToken);

        var keycloakIdentifierSystem = StaffAccountLinking.BuildKeycloakIdentifierSystem(keycloakAuthority);

        practitioner.Identifier ??= [];
        // Drop any pending invite code so the resource is treated as fully claimed.
        practitioner.Identifier = practitioner.Identifier
            .Where(i => !string.Equals(i.System, StaffAccountLinking.InviteCodeIdentifierSystem, StringComparison.OrdinalIgnoreCase))
            .ToList();

        StaffAccountLinking.AddOrReplaceIdentifier(practitioner.Identifier, keycloakIdentifierSystem, keycloakUserId);
        StaffAccountLinking.AddOrReplaceIdentifier(practitioner.Identifier, $"{keycloakIdentifierSystem}/username", email);

        await _repository.UpdateAsync(practitioner.Id, practitioner, _practitionerIndexer);

        return Ok(new ProvisionAccountResponse
        {
            PractitionerId = practitioner.Id,
            KeycloakUserId = keycloakUserId,
            Username = email,
            Password = password,
            AssignedRealmRole = realmRoleToAssign
        });
    }

    public sealed class ProvisionAccountRequest
    {
        public string PractitionerId { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
    }

    public sealed class ProvisionAccountResponse
    {
        public string PractitionerId { get; set; } = string.Empty;
        public string KeycloakUserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string AssignedRealmRole { get; set; } = string.Empty;
    }
}
