using System.ComponentModel.DataAnnotations;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Organizations.Models;

public sealed class OrganizationEditModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Alias { get; set; }

    public bool Active { get; set; } = true;

    [MaxLength(200)]
    public string? IdentifierSystem { get; set; }

    [MaxLength(200)]
    public string? IdentifierValue { get; set; }

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? AddressLine { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    public static OrganizationEditModel FromOrganization(Organization organization)
    {
        var identifier = organization.Identifier.FirstOrDefault();
        var contact = organization.Contact.FirstOrDefault();

        var email = contact?.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email)?.Value;
        var phone = contact?.Telecom.FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone)?.Value;
        var line = contact?.Address?.Line?.FirstOrDefault();

        return new OrganizationEditModel
        {
            Id = organization.Id,
            Name = organization.Name ?? string.Empty,
            Alias = organization.Alias.FirstOrDefault(),
            Active = organization.Active ?? true,
            IdentifierSystem = identifier?.System,
            IdentifierValue = identifier?.Value,
            Email = email,
            Phone = phone,
            AddressLine = line,
            City = contact?.Address?.City,
            State = contact?.Address?.State,
            PostalCode = contact?.Address?.PostalCode,
            Country = contact?.Address?.Country
        };
    }

    public Organization ToFhirOrganization()
    {
        var organization = new Organization
        {
            Id = Id,
            Name = Name,
            Active = Active
        };

        if (!string.IsNullOrWhiteSpace(Alias))
        {
            organization.Alias = [Alias.Trim()];
        }

        if (!string.IsNullOrWhiteSpace(IdentifierSystem) && !string.IsNullOrWhiteSpace(IdentifierValue))
        {
            organization.Identifier =
            [
                new Identifier
                {
                    System = IdentifierSystem,
                    Value = IdentifierValue
                }
            ];
        }

        var contactPoints = new List<ContactPoint>();

        if (!string.IsNullOrWhiteSpace(Email))
        {
            contactPoints.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Email,
                Value = Email.Trim()
            });
        }

        if (!string.IsNullOrWhiteSpace(Phone))
        {
            contactPoints.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = Phone.Trim()
            });
        }

        var hasAddress = !string.IsNullOrWhiteSpace(AddressLine)
            || !string.IsNullOrWhiteSpace(City)
            || !string.IsNullOrWhiteSpace(State)
            || !string.IsNullOrWhiteSpace(PostalCode)
            || !string.IsNullOrWhiteSpace(Country);

        if (contactPoints.Count > 0 || hasAddress)
        {
            var contact = new ExtendedContactDetail
            {
                Telecom = contactPoints
            };

            if (hasAddress)
            {
                contact.Address = new Address
                {
                    Line = string.IsNullOrWhiteSpace(AddressLine) ? [] : [AddressLine.Trim()],
                    City = City?.Trim(),
                    State = State?.Trim(),
                    PostalCode = PostalCode?.Trim(),
                    Country = Country?.Trim()
                };
            }

            organization.Contact = [contact];
        }

        return organization;
    }
}
