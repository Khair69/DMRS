using DMRS.Client.Features.Organizations.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Organizations.Services;

public class OrganizationFeatureService : FhirFeatureServiceBase<Organization, OrganizationEditModel, OrganizationSummaryViewModel>
{
    public OrganizationFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override Organization ToResource(OrganizationEditModel model)
        => model.ToFhirOrganization();

    protected override OrganizationSummaryViewModel MapToSummary(Organization organization)
    {
        var identifier = organization.Identifier.FirstOrDefault();
        var email = organization.Contact
            .SelectMany(c => c.Telecom)
            .FirstOrDefault(t => t.System == ContactPoint.ContactPointSystem.Email)
            ?.Value;

        return new OrganizationSummaryViewModel(
            organization.Id ?? "(no-id)",
            string.IsNullOrWhiteSpace(organization.Name) ? "Unnamed organization" : organization.Name,
            organization.Active ?? false,
            identifier?.Value,
            email);
    }
}
