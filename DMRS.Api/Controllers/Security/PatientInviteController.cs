using System.Security.Cryptography;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Persistence;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Security;

[ApiController]
[Route("api/patients")]
[Authorize]
public sealed class PatientInviteController : ControllerBase
{
    private const string InviteCodeIdentifierSystem = "https://dmrs.local/invites/patient";

    private readonly IFhirRepository _repository;
    private readonly PatientIndexer _patientIndexer;
    private readonly ISmartAuthorizationService _authorizationService;
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public PatientInviteController(
        IFhirRepository repository,
        PatientIndexer patientIndexer,
        ISmartAuthorizationService authorizationService,
        AppDbContext dbContext,
        IConfiguration configuration)
    {
        _repository = repository;
        _patientIndexer = patientIndexer;
        _authorizationService = authorizationService;
        _dbContext = dbContext;
        _configuration = configuration;
    }

    [HttpPost("create-invite")]
    public async Task<IActionResult> CreateInvite([FromBody] CreatePatientInviteRequest request, CancellationToken cancellationToken)
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
            || request.BirthDate is null)
        {
            return BadRequest("GivenName, FamilyName and BirthDate are required.");
        }

        if (string.IsNullOrWhiteSpace(request.AppBaseUri))
        {
            return BadRequest("AppBaseUri is required.");
        }

        var patientAccess = _authorizationService.GetAccessLevel(User, "Patient", "write");
        if (patientAccess == SmartAccessLevel.None || patientAccess == SmartAccessLevel.Patient)
        {
            return Forbid();
        }

        var accessLevel = patientAccess == SmartAccessLevel.System
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

        var patient = BuildPatient(request);

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var patientId = await _repository.CreateAsync(patient, _patientIndexer);

            var inviteCode = GenerateInviteCode();
            patient.Identifier ??= [];
            patient.Identifier.Add(new Identifier
            {
                System = InviteCodeIdentifierSystem,
                Value = inviteCode
            });

            await _repository.UpdateAsync(patientId, patient, _patientIndexer);
            await tx.CommitAsync(cancellationToken);

            var claimPath = NormalizeClaimPath(request.ClaimPath);
            var claimLink = $"{request.AppBaseUri.TrimEnd('/')}{claimPath}?code={Uri.EscapeDataString(inviteCode)}";
            var autoClaimLink = $"{claimLink}&auto=true";
            var registrationLink = BuildKeycloakRegistrationUrl(autoClaimLink);

            return Ok(new CreatePatientInviteResponse
            {
                PatientId = patientId,
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

    [HttpPost("{id}/create-invite")]
    public async Task<IActionResult> CreateInviteForExisting(string id, [FromBody] GeneratePatientInviteRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest("Patient id is required.");
        }

        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.AppBaseUri))
        {
            return BadRequest("AppBaseUri is required.");
        }

        var patientAccess = _authorizationService.GetAccessLevel(User, "Patient", "write");
        if (patientAccess == SmartAccessLevel.None || patientAccess == SmartAccessLevel.Patient)
        {
            return Forbid();
        }

        var patient = await _repository.GetAsync<Patient>(id.Trim());
        if (patient is null)
        {
            return NotFound($"Patient/{id} was not found.");
        }

        if (patientAccess == SmartAccessLevel.User)
        {
            var callerOrganizations = await _authorizationService.ResolveOrganizationIdsAsync(User);
            var owned = await _authorizationService.IsResourceOwnedByOrganizationsAsync(patient, callerOrganizations);
            if (!owned)
            {
                return Forbid();
            }
        }

        if (IsLinkedToKeycloak(patient, _configuration["Keycloak:Authority"]))
        {
            return Conflict("Patient is already linked to a Keycloak account.");
        }

        patient.Identifier ??= [];
        patient.Identifier = patient.Identifier
            .Where(i => !string.Equals(i.System, InviteCodeIdentifierSystem, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var inviteCode = GenerateInviteCode();
        patient.Identifier.Add(new Identifier
        {
            System = InviteCodeIdentifierSystem,
            Value = inviteCode
        });

        await _repository.UpdateAsync(patient.Id, patient, _patientIndexer);

        var claimPath = NormalizeClaimPath(request.ClaimPath);
        var claimLink = $"{request.AppBaseUri.TrimEnd('/')}{claimPath}?code={Uri.EscapeDataString(inviteCode)}";
        var autoClaimLink = $"{claimLink}&auto=true";
        var registrationLink = BuildKeycloakRegistrationUrl(autoClaimLink);

        return Ok(new CreatePatientInviteResponse
        {
            PatientId = patient.Id,
            InviteCode = inviteCode,
            ClaimLink = claimLink,
            RegistrationLink = registrationLink
        });
    }

    private static Patient BuildPatient(CreatePatientInviteRequest request)
    {
        var patient = new Patient
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
            Gender = ParseGender(request.Gender),
            BirthDate = request.BirthDate?.ToString("yyyy-MM-dd"),
            ManagingOrganization = new ResourceReference($"Organization/{request.OrganizationId.Trim()}")
        };

        if (!string.IsNullOrWhiteSpace(request.IdentifierSystem) && !string.IsNullOrWhiteSpace(request.IdentifierValue))
        {
            patient.Identifier =
            [
                new Identifier
                {
                    System = request.IdentifierSystem.Trim(),
                    Value = request.IdentifierValue.Trim()
                }
            ];
        }

        return patient;
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

    private static AdministrativeGender ParseGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AdministrativeGender.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "male" => AdministrativeGender.Male,
            "female" => AdministrativeGender.Female,
            "other" => AdministrativeGender.Other,
            _ => AdministrativeGender.Unknown
        };
    }

    private static string GenerateInviteCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToHexString(bytes);
    }

    private static bool IsLinkedToKeycloak(Patient patient, string? keycloakAuthority)
    {
        var keycloakSystem = BuildKeycloakIdentifierSystem(keycloakAuthority);
        return patient.Identifier.Any(i => string.Equals(i.System, keycloakSystem, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildKeycloakIdentifierSystem(string? keycloakAuthority)
    {
        if (string.IsNullOrWhiteSpace(keycloakAuthority))
        {
            return "https://keycloak.local/users";
        }

        return $"{keycloakAuthority.TrimEnd('/')}/users";
    }

    private static string NormalizeClaimPath(string? claimPath)
    {
        if (string.IsNullOrWhiteSpace(claimPath))
        {
            return "/patients/claim";
        }

        return claimPath.StartsWith('/') ? claimPath : $"/{claimPath}";
    }

    public sealed class CreatePatientInviteRequest
    {
        public string OrganizationId { get; set; } = string.Empty;
        public string AppBaseUri { get; set; } = string.Empty;
        public string? ClaimPath { get; set; }
        public string GivenName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public string? Gender { get; set; }
        public string? IdentifierSystem { get; set; }
        public string? IdentifierValue { get; set; }
    }

    public sealed class GeneratePatientInviteRequest
    {
        public string AppBaseUri { get; set; } = string.Empty;
        public string? ClaimPath { get; set; }
    }

    public sealed class CreatePatientInviteResponse
    {
        public string PatientId { get; set; } = string.Empty;
        public string InviteCode { get; set; } = string.Empty;
        public string ClaimLink { get; set; } = string.Empty;
        public string RegistrationLink { get; set; } = string.Empty;
    }
}
