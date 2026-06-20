using System.ComponentModel.DataAnnotations;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Staff.Models;

/// <summary>
/// Editable demographic fields for an existing staff member (FHIR <see cref="Practitioner"/>).
/// Staff are created through the invite flow (<see cref="StaffInviteEditModel"/>); this model backs the
/// later "edit" of their name/contact details. <see cref="ApplyTo"/> mutates the loaded resource in place
/// so account-linking identifiers and any other elements are preserved.
/// </summary>
public sealed class StaffEditModel
{
    [Required]
    [MaxLength(100)]
    public string GivenName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FamilyName { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    public bool Active { get; set; } = true;

    public static StaffEditModel FromPractitioner(Practitioner practitioner)
    {
        var name = practitioner.Name.FirstOrDefault();

        return new StaffEditModel
        {
            GivenName = string.Join(" ", name?.Given ?? []).Trim(),
            FamilyName = name?.Family?.Trim() ?? string.Empty,
            Email = practitioner.Telecom
                .FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email)?.Value,
            Phone = practitioner.Telecom
                .FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Phone)?.Value,
            Active = practitioner.Active ?? true
        };
    }

    /// <summary>
    /// Writes the edited fields onto an existing practitioner, preserving identifiers (e.g. the Keycloak
    /// account link) and any contact points that aren't email/phone.
    /// </summary>
    public void ApplyTo(Practitioner practitioner)
    {
        practitioner.Active = Active;

        practitioner.Name =
        [
            new HumanName
            {
                Given = [GivenName.Trim()],
                Family = FamilyName.Trim()
            }
        ];

        // Keep any telecom that isn't an email/phone, then re-add the edited ones.
        var preserved = practitioner.Telecom
            .Where(t => t.System != ContactPoint.ContactPointSystem.Email
                     && t.System != ContactPoint.ContactPointSystem.Phone)
            .ToList();

        var telecom = new List<ContactPoint>(preserved);

        if (!string.IsNullOrWhiteSpace(Email))
        {
            telecom.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Email,
                Value = Email.Trim()
            });
        }

        if (!string.IsNullOrWhiteSpace(Phone))
        {
            telecom.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = Phone.Trim()
            });
        }

        practitioner.Telecom = telecom;
    }
}
