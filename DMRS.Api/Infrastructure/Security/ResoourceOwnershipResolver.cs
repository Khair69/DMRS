using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Security
{
    public interface IResourceOwnershipResolver
    {
        Task<IEnumerable<string>> ResolveOrganizationsAsync(Resource resource);
        Task<IEnumerable<string>> ResolveOrganizationsAsync(string resourceType, string resourceId);
    }

    public class ResourceOwnershipResolver : IResourceOwnershipResolver
    {
        private readonly IFhirRepository _repo;

        public ResourceOwnershipResolver(IFhirRepository repo)
        {
            _repo = repo;
        }

        public async Task<IEnumerable<string>> ResolveOrganizationsAsync(Resource resource)
        {
            switch (resource)
            {
                case Organization org:
                    return string.IsNullOrWhiteSpace(org.Id) ? Enumerable.Empty<string>() : [org.Id];

                case PractitionerRole role:
                    return ReferenceToIds(role.Organization?.Reference, "Organization");

                case Location loc:
                    return ReferenceToIds(loc.ManagingOrganization?.Reference, "Organization");

                case HealthcareService svc:
                    return ReferenceToIds(svc.ProvidedBy?.Reference, "Organization");

                case Patient patient:
                    return ReferenceToIds(patient.ManagingOrganization?.Reference, "Organization");

                case Practitioner practitioner:
                    if (string.IsNullOrWhiteSpace(practitioner.Id))
                    {
                        return Enumerable.Empty<string>();
                    }

                    return await ResolvePractitionerOrganizations(practitioner.Id);

                default:
                    return Enumerable.Empty<string>();
            }
        }

        public async Task<IEnumerable<string>> ResolveOrganizationsAsync(string resourceType, string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceType) || string.IsNullOrWhiteSpace(resourceId))
            {
                return Enumerable.Empty<string>();
            }

            return resourceType.Trim() switch
            {
                "Organization" => [resourceId],
                "Patient" => await ResolveByResourceAsync<Patient>(resourceId),
                "Practitioner" => await ResolvePractitionerOrganizations(resourceId),
                "PractitionerRole" => await ResolveByResourceAsync<PractitionerRole>(resourceId),
                "Location" => await ResolveByResourceAsync<Location>(resourceId),
                "HealthcareService" => await ResolveByResourceAsync<HealthcareService>(resourceId),
                _ => Enumerable.Empty<string>()
            };
        }

        private async Task<IEnumerable<string>> ResolveByResourceAsync<T>(string resourceId) where T : Resource
        {
            var resource = await _repo.GetAsync<T>(resourceId);
            if (resource is null)
            {
                return Enumerable.Empty<string>();
            }

            return await ResolveOrganizationsAsync(resource);
        }

        private async Task<IEnumerable<string>> ResolvePractitionerOrganizations(string practitionerId)
        {
            var queryParams = new Dictionary<string, string>
            {
                { "practitioner", $"Practitioner/{practitionerId}" }
            };

            var roles = await _repo.SearchAsync<PractitionerRole>(queryParams);

            return roles
                .SelectMany(r => ReferenceToIds(r.Organization?.Reference, "Organization"))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> ReferenceToIds(string? reference, string expectedType)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return Enumerable.Empty<string>();
            }

            var trimmed = reference.Trim();
            var prefix = $"{expectedType}/";
            if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Enumerable.Empty<string>();
            }

            var id = trimmed[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                return Enumerable.Empty<string>();
            }

            return [id];
        }
    }
}
