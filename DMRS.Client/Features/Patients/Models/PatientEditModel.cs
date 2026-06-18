using System.ComponentModel.DataAnnotations;
using DMRS.Shared.Constants;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Patients.Models;

public sealed class PatientEditModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FamilyName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string GivenName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Gender { get; set; }

    [Required]
    public DateTime? BirthDate { get; set; }

    [MaxLength(60)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? IdentifierSystem { get; set; }

    [MaxLength(100)]
    public string? IdentifierValue { get; set; }

    [MaxLength(100)]
    public string? ManagingOrganizationId { get; set; }

    // The system/value of the identifier that was surfaced into the editable fields when this model
    // was loaded. Used by the update flow to replace exactly that identifier while preserving every
    // other identifier on the resource (patient number, invite code, Keycloak account link, ...).
    public string? OriginalIdentifierSystem { get; set; }
    public string? OriginalIdentifierValue { get; set; }

    public static PatientEditModel FromPatient(Patient patient)
    {
        var name = patient.Name.FirstOrDefault();
        // The patient number is system-managed and immutable, so never surface it in the editable
        // identifier fields. Prefer the national number; otherwise the first non-patient-number id.
        var editable = patient.Identifier.FirstOrDefault(i => i.System == PatientIdentifierSystems.NationalId)
            ?? patient.Identifier.FirstOrDefault(i => i.System != PatientIdentifierSystems.PatientNumber);
        var city = patient.Address?.FirstOrDefault()?.City;
        var managingOrganizationId = ParseReferenceId(patient.ManagingOrganization?.Reference, "organization");

        DateTime? birthDate = null;
        if (DateTime.TryParse(patient.BirthDate, out var parsedBirthDate))
        {
            birthDate = parsedBirthDate;
        }

        return new PatientEditModel
        {
            Id = patient.Id,
            FamilyName = name?.Family ?? string.Empty,
            GivenName = name?.Given?.FirstOrDefault() ?? string.Empty,
            Gender = patient.Gender.ToString(),
            BirthDate = birthDate,
            City = city,
            IdentifierSystem = editable?.System,
            IdentifierValue = editable?.Value,
            OriginalIdentifierSystem = editable?.System,
            OriginalIdentifierValue = editable?.Value,
            ManagingOrganizationId = managingOrganizationId
        };
    }

    public Patient ToFhirPatient()
    {
        var patient = new Patient
        {
            Id = Id,
            Name =
            [
                new HumanName
                {
                    Family = FamilyName,
                    Given = [GivenName]
                }
            ],
            Gender = ParseGender(Gender),
            BirthDate = BirthDate?.ToString("yyyy-MM-dd")
        };

        if (!string.IsNullOrWhiteSpace(City))
        {
            patient.Address =
            [
                new Address { City = City.Trim(), Country = "SY" }
            ];
        }

        if (!string.IsNullOrWhiteSpace(IdentifierSystem) && !string.IsNullOrWhiteSpace(IdentifierValue))
        {
            patient.Identifier =
            [
                new Identifier
                {
                    System = IdentifierSystem,
                    Value = IdentifierValue
                }
            ];
        }

        if (!string.IsNullOrWhiteSpace(ManagingOrganizationId))
        {
            patient.ManagingOrganization = new ResourceReference($"Organization/{ManagingOrganizationId}");
        }

        return patient;
    }

    private static string? ParseReferenceId(string? value, string expectedResourceType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var prefix = $"{expectedResourceType}/";
        if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[prefix.Length..];
        }

        if (trimmed.Contains('/'))
        {
            return null;
        }

        return trimmed;
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
}
