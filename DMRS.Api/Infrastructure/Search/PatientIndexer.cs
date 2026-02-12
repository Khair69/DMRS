using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search
{
    public class PatientIndexer : ISearchIndexer
    {
        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is Patient patient)
            {
                // Index the ID
                indices.Add(new ResourceIndex
                {
                    ResourceType = "Patient",
                    ResourceId = patient.Id,
                    SearchParamCode = "_id",
                    Value = patient.Id
                });

                // Index Gender
                if (patient.Gender.HasValue)
                {
                    indices.Add(new ResourceIndex
                    {
                        ResourceType = "Patient",
                        ResourceId = patient.Id,
                        SearchParamCode = "gender",
                        Value = patient.Gender.ToString().ToLower()
                    });
                }

                // Index Family Name (Last Name)
                foreach (var name in patient.Name)
                {
                    if (!string.IsNullOrEmpty(name.Family))
                    {
                        indices.Add(new ResourceIndex
                        {
                            ResourceType = "Patient",
                            ResourceId = patient.Id,
                            SearchParamCode = "family",
                            Value = name.Family.ToLower()
                        });
                    }
                    if (!string.IsNullOrEmpty(name.Given.First()))
                    {
                        indices.Add(new ResourceIndex
                        {
                            ResourceType = "Patient",
                            ResourceId = patient.Id,
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
