using DMRS.Client.Features.Locations.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Locations.Services;

public sealed class LocationFeatureService : FhirFeatureServiceBase<Location, LocationEditModel, LocationSummaryViewModel>
{
    public LocationFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override Location ToResource(LocationEditModel model)
        => model.ToFhirLocation();

    protected override LocationSummaryViewModel MapToSummary(Location location)
    {
        var managingOrgId = FhirReferenceHelper.ExtractReferenceId(location.ManagingOrganization?.Reference, "organization");

        return new LocationSummaryViewModel(
            location.Id ?? "(no-id)",
            location.Name ?? "(no-name)",
            location.Status?.ToString() ?? "unknown",
            managingOrgId);
    }
}
