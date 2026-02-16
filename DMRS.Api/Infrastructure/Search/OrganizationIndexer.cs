using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search
{
    public class OrganizationIndexer : ISearchIndexer
    {
        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is Organization organization)
            {
                indices.Add(new ResourceIndex
                {
                    ResourceType = "Organization",
                    ResourceId = organization.Id,
                    SearchParamCode = "_id",
                    Value = organization.Id
                });

                indices.Add(new ResourceIndex
                {
                    ResourceType = "Oranization",
                    ResourceId = organization.Id,
                    SearchParamCode = "_lastUpdated",
                    Value = organization.Meta.LastUpdated?.ToString("o") ?? string.Empty
                });

                indices.Add(new ResourceIndex
                {
                    ResourceType = "Organization",
                    ResourceId = organization.Id,
                    SearchParamCode = "name",
                    Value = organization.Name.ToLower()
                });

                //indices.Add(new ResourceIndex
                //{
                //    ResourceType = "Organization",
                //    ResourceId = organization.Id,
                //    SearchParamCode = "address",
                //    Value = organization.Contact.
                //});
            }
            return indices;

        }
    }
}
