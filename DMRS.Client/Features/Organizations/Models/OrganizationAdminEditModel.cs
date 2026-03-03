using System.ComponentModel.DataAnnotations;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Organizations.Models;

public sealed class OrganizationAdminEditModel
{
    [Required]
    [MaxLength(100)]
    public string GivenName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FamilyName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? IdentifierSystem { get; set; }

    [MaxLength(200)]
    public string? IdentifierValue { get; set; }

    [MaxLength(200)]
    public string RoleSystem { get; set; } = "https://dmrs.local/fhir/practitioner-role";

    [MaxLength(100)]
    public string RoleCode { get; set; } = "ORG_ADMIN";

    [MaxLength(200)]
    public string RoleDisplay { get; set; } = "Organization Administrator";

    public Practitioner ToPractitioner()
    {
        var practitioner = new Practitioner
        {
            Active = true,
            Name =
            [
                new HumanName
                {
                    Given = [GivenName.Trim()],
                    Family = FamilyName.Trim()
                }
            ],
            Telecom =
            [
                new ContactPoint
                {
                    System = ContactPoint.ContactPointSystem.Email,
                    Value = Email.Trim()
                }
            ]
        };

        if (!string.IsNullOrWhiteSpace(Phone))
        {
            practitioner.Telecom.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = Phone.Trim()
            });
        }

        var identifiers = new List<Identifier>();

        if (!string.IsNullOrWhiteSpace(IdentifierSystem) && !string.IsNullOrWhiteSpace(IdentifierValue))
        {
            identifiers.Add(new Identifier
            {
                System = IdentifierSystem,
                Value = IdentifierValue
            });
        }

        if (identifiers.Count > 0)
        {
            practitioner.Identifier = identifiers;
        }

        return practitioner;
    }

    public PractitionerRole ToPractitionerRole(string practitionerId, string organizationId)
    {
        return new PractitionerRole
        {
            Active = true,
            Practitioner = new ResourceReference($"Practitioner/{practitionerId}"),
            Organization = new ResourceReference($"Organization/{organizationId}"),
            Code =
            [
                new CodeableConcept
                {
                    Coding =
                    [
                        new Coding(RoleSystem, RoleCode, RoleDisplay)
                    ],
                    Text = RoleDisplay
                }
            ]
        };
    }
}
