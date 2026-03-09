using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Persistence;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace DMRS.Api.Controllers.Security;

[ApiController]
[Route("api/staff")]
[Authorize]
public sealed class StaffInviteController : ControllerBase
{
    private const string InviteCodeIdentifierSystem = "https://dmrs.local/invites/practitioner";

    private readonly IFhirRepository _repository;
    private readonly PractitionerIndexer _practitionerIndexer;
    private readonly PractitionerRoleIndexer _practitionerRoleIndexer;
    private readonly ISmartAuthorizationService _authorizationService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public StaffInviteController(
        IFhirRepository repository,
        PractitionerIndexer practitionerIndexer,
        PractitionerRoleIndexer practitionerRoleIndexer,
        ISmartAuthorizationService authorizationService,
        AppDbContext dbContext,
        IConfiguration configuration)
    {
        _repository = repository;
        _practitionerIndexer = practitionerIndexer;
        _practitionerRoleIndexer = practitionerRoleIndexer;
        _authorizationService = authorizationService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPost("create-invite")]
    public async Task<IActionResult> CreateInvite([FromBody] CreateStaffInviteRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OrganizationId))
        {
            return BadRequest("Organization id is required.");
        }

        if (string.IsNullOrWhiteSpace(request.GivenName)
            || string.IsNullOrWhiteSpace(request.FamilyName)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("GivenName, FamilyName and Email are required.");
        }

        if (string.IsNullOrWhiteSpace(request.AppBaseUri))
        {
            return BadRequest("AppBaseUri is required.");
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
        if (accessLevel == SmartAccessLevel.User)
        {
            var callerOrganizations = await _authorizationService.ResolveOrganizationIdsAsync(User);
            if (!callerOrganizations.Contains(organizationId, StringComparer.OrdinalIgnoreCase))
            {
                return Forbid();
            }
        }

        var organization = await _repository.GetAsync<Organization>(organizationId);
        if (organization is null)
        {
            return NotFound($"Organization/{organizationId} was not found.");
        }

        var practitioner = BuildPractitioner(request);
        var practitionerRole = BuildPractitionerRole(request, organizationId);

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var practitionerId = await _repository.CreateAsync(practitioner, _practitionerIndexer);
            practitionerRole.Practitioner = new ResourceReference($"Practitioner/{practitionerId}");

            var practitionerRoleId = await _repository.CreateAsync(practitionerRole, _practitionerRoleIndexer);

            var inviteCode = GenerateInviteCode();
            practitioner.Identifier ??= [];
            practitioner.Identifier.Add(new Identifier
            {
                System = InviteCodeIdentifierSystem,
                Value = inviteCode
            });

            await _repository.UpdateAsync(practitionerId, practitioner, _practitionerIndexer);

            await tx.CommitAsync(cancellationToken);

            var claimPath = NormalizeClaimPath(request.ClaimPath);
            var claimLink = $"{request.AppBaseUri.TrimEnd('/')}{claimPath}?code={Uri.EscapeDataString(inviteCode)}";
            var autoClaimLink = $"{claimLink}&auto=true";
            var registrationLink = BuildKeycloakRegistrationUrl(autoClaimLink);

            return Ok(new CreateStaffInviteResponse
            {
                PractitionerId = practitionerId,
                PractitionerRoleId = practitionerRoleId,
                InviteCode = inviteCode,
                ClaimLink = claimLink,
                RegistrationLink = registrationLink
            });
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private Practitioner BuildPractitioner(CreateStaffInviteRequest request)
    {
        var practitioner = new Practitioner
        {
            Active = true,
            Name =
            [
                new HumanName
                {
                    Given = [request.GivenName.Trim()],
                    Family = request.FamilyName.Trim()
                }
            ],
            Telecom =
            [
                new ContactPoint
                {
                    System = ContactPoint.ContactPointSystem.Email,
                    Value = request.Email.Trim()
                }
            ]
        };

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            practitioner.Telecom.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = request.Phone.Trim()
            });
        }

        if (!string.IsNullOrWhiteSpace(request.IdentifierSystem) && !string.IsNullOrWhiteSpace(request.IdentifierValue))
        {
            practitioner.Identifier =
            [
                new Identifier
                {
                    System = request.IdentifierSystem.Trim(),
                    Value = request.IdentifierValue.Trim()
                }
            ];
        }

        return practitioner;
    }

    private static PractitionerRole BuildPractitionerRole(CreateStaffInviteRequest request, string organizationId)
    {
        return new PractitionerRole
        {
            Active = true,
            Organization = new ResourceReference($"Organization/{organizationId}"),
            Code =
            [
                new CodeableConcept
                {
                    Coding =
                    [
                        new Coding(
                            request.RoleSystem?.Trim() ?? "https://dmrs.local/fhir/practitioner-role",
                            request.RoleCode?.Trim() ?? "DOCTOR",
                            request.RoleDisplay?.Trim() ?? "Doctor")
                    ],
                    Text = request.RoleDisplay?.Trim() ?? "Doctor"
                }
            ]
        };
    }

    private string BuildKeycloakRegistrationUrl(string redirectUri)
    {
        var keycloakAuthority = _configuration["Keycloak:Authority"] ?? "http://localhost:8080/realms/DMRS";
        var keycloakClientId = _configuration["Keycloak:ClientId"] ?? "dmrs-api";
        var authBase = keycloakAuthority.TrimEnd('/');

        return $"{authBase}/protocol/openid-connect/registrations"
            + $"?client_id={Uri.EscapeDataString(keycloakClientId)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString("openid profile")}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}";
    }

    private static string GenerateInviteCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeClaimPath(string? claimPath)
    {
        if (string.IsNullOrWhiteSpace(claimPath))
        {
            return "/staff/claim";
        }

        return claimPath.StartsWith('/') ? claimPath : $"/{claimPath}";
    }

    public sealed class CreateStaffInviteRequest
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

    public sealed class CreateStaffInviteResponse
    {
        public string PractitionerId { get; set; } = string.Empty;
        public string PractitionerRoleId { get; set; } = string.Empty;
        public string InviteCode { get; set; } = string.Empty;
        public string ClaimLink { get; set; } = string.Empty;
        public string RegistrationLink { get; set; } = string.Empty;
    }
}
