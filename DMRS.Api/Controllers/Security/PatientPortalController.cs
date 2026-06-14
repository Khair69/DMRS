using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Security;

/// <summary>
/// Self-service endpoints for a logged-in patient ("me"). A patient's SMART scope is
/// <c>patient/*.read</c> only, so they cannot write their own record through the FHIR controllers.
/// These endpoints resolve the caller's own Patient via <see cref="ISmartAuthorizationService"/> and
/// let them read it and edit a safe subset of demographics — never organization, identifiers, links,
/// or general practitioner.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public sealed class PatientPortalController : ControllerBase
{
    private static readonly string[] EditableTelecomSystems = ["phone", "email"];

    private readonly IFhirRepository _repository;
    private readonly PatientIndexer _patientIndexer;
    private readonly ISmartAuthorizationService _authorizationService;
    private readonly FhirJsonSerializer _serializer;

    public PatientPortalController(
        IFhirRepository repository,
        PatientIndexer patientIndexer,
        ISmartAuthorizationService authorizationService,
        FhirJsonSerializer serializer)
    {
        _repository = repository;
        _patientIndexer = patientIndexer;
        _authorizationService = authorizationService;
        _serializer = serializer;
    }

    /// <summary>Returns the caller's own Patient resource as FHIR JSON.</summary>
    [HttpGet("patient")]
    public async Task<IActionResult> GetMyPatient()
    {
        var patient = await ResolveMyPatientAsync();
        if (patient is null)
        {
            return NotFound("No patient record is linked to your account.");
        }

        return Content(_serializer.SerializeToString(patient), "application/fhir+json");
    }

    /// <summary>
    /// Updates a safe subset of the caller's own demographics. Identity-critical fields
    /// (identifiers, managingOrganization, link, generalPractitioner) are preserved untouched.
    /// </summary>
    [HttpPut("patient")]
    public async Task<IActionResult> UpdateMyPatient([FromBody] UpdateMyProfileRequest request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var patient = await ResolveMyPatientAsync();
        if (patient is null)
        {
            return NotFound("No patient record is linked to your account.");
        }

        ApplyEditableFields(patient, request);

        await _repository.UpdateAsync(patient.Id!, patient, _patientIndexer);

        return Content(_serializer.SerializeToString(patient), "application/fhir+json");
    }

    private async Task<Patient?> ResolveMyPatientAsync()
    {
        var patientId = _authorizationService.ResolvePatientId(User);
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return null;
        }

        return await _repository.GetAsync<Patient>(patientId);
    }

    private static void ApplyEditableFields(Patient patient, UpdateMyProfileRequest request)
    {
        // Name: keep a single official name in slot 0, preserving any others untouched.
        if (!string.IsNullOrWhiteSpace(request.FamilyName) || !string.IsNullOrWhiteSpace(request.GivenName))
        {
            var name = patient.Name.FirstOrDefault();
            if (name is null)
            {
                name = new HumanName { Use = HumanName.NameUse.Official };
                patient.Name.Insert(0, name);
            }

            if (request.FamilyName is not null)
            {
                name.Family = request.FamilyName.Trim();
            }

            if (request.GivenName is not null)
            {
                name.Given = request.GivenName
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        if (request.BirthDate is not null)
        {
            patient.BirthDate = string.IsNullOrWhiteSpace(request.BirthDate) ? null : request.BirthDate.Trim();
        }

        if (request.Gender is not null)
        {
            patient.Gender = string.IsNullOrWhiteSpace(request.Gender)
                ? null
                : EnumUtility.ParseLiteral<AdministrativeGender>(request.Gender.Trim().ToLowerInvariant());
        }

        if (request.MaritalStatus is not null)
        {
            patient.MaritalStatus = BuildMaritalStatus(request.MaritalStatus);
        }

        // Telecom: replace only the phone/email entries we manage; leave any other contact points.
        if (request.Phone is not null || request.Email is not null)
        {
            patient.Telecom = patient.Telecom
                .Where(c => c.System is null
                    || !EditableTelecomSystems.Contains(c.System.Value.ToString().ToLowerInvariant()))
                .ToList();

            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                patient.Telecom.Add(new ContactPoint
                {
                    System = ContactPoint.ContactPointSystem.Phone,
                    Value = request.Phone.Trim()
                });
            }

            if (!string.IsNullOrWhiteSpace(request.Email))
            {
                patient.Telecom.Add(new ContactPoint
                {
                    System = ContactPoint.ContactPointSystem.Email,
                    Value = request.Email.Trim()
                });
            }
        }

        if (request.Address is not null)
        {
            var address = BuildAddress(request.Address);
            // Don't replace an existing address with an empty element when every field is blank.
            patient.Address = IsEmptyAddress(address) ? [] : [address];
        }
    }

    private static bool IsEmptyAddress(Address address)
        => (address.Line is null || !address.Line.Any())
            && string.IsNullOrWhiteSpace(address.City)
            && string.IsNullOrWhiteSpace(address.State)
            && string.IsNullOrWhiteSpace(address.PostalCode)
            && string.IsNullOrWhiteSpace(address.Country);

    private static CodeableConcept? BuildMaritalStatus(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return new CodeableConcept(
            "http://terminology.hl7.org/CodeSystem/v3-MaritalStatus",
            code.Trim().ToUpperInvariant());
    }

    private static Address BuildAddress(AddressDto dto)
    {
        var address = new Address
        {
            City = NullIfBlank(dto.City),
            State = NullIfBlank(dto.State),
            PostalCode = NullIfBlank(dto.PostalCode),
            Country = NullIfBlank(dto.Country)
        };

        if (!string.IsNullOrWhiteSpace(dto.Line))
        {
            address.Line = dto.Line
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return address;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed class UpdateMyProfileRequest
    {
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? BirthDate { get; set; }
        public string? Gender { get; set; }
        public string? MaritalStatus { get; set; }
        public AddressDto? Address { get; set; }
    }

    public sealed class AddressDto
    {
        public string? Line { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
    }
}
