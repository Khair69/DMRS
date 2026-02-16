using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search
{
    public class PractitionerIndexer : ISearchIndexer
    {
        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is Practitioner practitioner)
            {
                indices.Add(new ResourceIndex
                {
                    ResourceType = "Practitioner",
                    ResourceId = practitioner.Id,
                    SearchParamCode = "_id",
                    Value = practitioner.Id
                });

                indices.Add(new ResourceIndex
                {
                    ResourceType = "Practitioner",
                    ResourceId = practitioner.Id,
                    SearchParamCode = "_lastUpdated",
                    Value = practitioner.Meta.LastUpdated?.ToString("o") ?? string.Empty
                });

                if (practitioner.Gender.HasValue)
                {
                    indices.Add(new ResourceIndex
                    {
                        ResourceType = "Practitioner",
                        ResourceId = practitioner.Id,
                        SearchParamCode = "gender",
                        Value = practitioner.Gender.ToString().ToLower()
                    });
                }

                //indices.Add(new ResourceIndex
                //{
                //    ResourceType = "Patient",
                //    ResourceId = patient.Id,
                //    SearchParamCode = "nationalId",
                //    Value = patient.Identifier
                //});

                foreach (var name in practitioner.Name)
                {
                    if (!string.IsNullOrEmpty(name.Family))
                    {
                        indices.Add(new ResourceIndex
                        {
                            ResourceType = "Practitioner",
                            ResourceId = practitioner.Id,
                            SearchParamCode = "family",
                            Value = name.Family.ToLower()
                        });
                    }
                    if (!string.IsNullOrEmpty(name.Given.First()))
                    {
                        indices.Add(new ResourceIndex
                        {
                            ResourceType = "Practitioner",
                            ResourceId = practitioner.Id,
                            SearchParamCode = "given",
                            Value = name.Given.First().ToLower()
                        });
                    }
                }
            }
            return indices;

        }
    }
}
