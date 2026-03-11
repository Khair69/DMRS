using System.ComponentModel.DataAnnotations;
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

    [MaxLength(100)]
    public string? IdentifierSystem { get; set; }

    [MaxLength(100)]
    public string? IdentifierValue { get; set; }

    [MaxLength(100)]
    public string? ManagingOrganizationId { get; set; }

    public static PatientEditModel FromPatient(Patient patient)
    {
        var name = patient.Name.FirstOrDefault();
        var identifier = patient.Identifier.FirstOrDefault();
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
            IdentifierSystem = identifier?.System,
            IdentifierValue = identifier?.Value,
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
