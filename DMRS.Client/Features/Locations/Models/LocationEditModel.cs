using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Locations.Models;

public sealed class LocationEditModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = "active";

    [MaxLength(100)]
    public string? ManagingOrganizationId { get; set; }

    public static LocationEditModel FromLocation(Location location)
    {
        var managingOrgId = FhirReferenceHelper.ExtractReferenceId(location.ManagingOrganization?.Reference, "organization");

        return new LocationEditModel
        {
            Id = location.Id,
            Name = location.Name ?? string.Empty,
            Status = location.Status?.ToString().ToLowerInvariant() ?? "unknown",
            ManagingOrganizationId = managingOrgId
        };
    }

    public Location ToFhirLocation()
    {
        var location = new Location
        {
            Id = Id,
            Name = Name,
            Status = ParseStatus(Status)
        };

        var orgRef = FhirReferenceHelper.NormalizeReference(ManagingOrganizationId, "Organization");
        if (!string.IsNullOrWhiteSpace(orgRef))
        {
            location.ManagingOrganization = new ResourceReference(orgRef);
        }

        return location;
    }

    private static Location.LocationStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Location.LocationStatus.Active;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "active" => Location.LocationStatus.Active,
            "suspended" => Location.LocationStatus.Suspended,
            "inactive" => Location.LocationStatus.Inactive,
            _ => Location.LocationStatus.Active
        };
    }
}
