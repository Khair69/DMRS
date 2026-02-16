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
                indices.Add(new ResourceIndex
                {
                    ResourceType = "Patient",
                    ResourceId = patient.Id,
                    SearchParamCode = "_id",
                    Value = patient.Id
                });

                indices.Add(new ResourceIndex
                {
                    ResourceType = "Patient",
                    ResourceId = patient.Id,
                    SearchParamCode = "_lastUpdated",
                    Value = patient.Meta.LastUpdated?.ToString("o") ?? string.Empty
                });

                indices.Add(new ResourceIndex
                {
                    ResourceType = "Patient",
                    ResourceId = patient.Id,
                    SearchParamCode = "birthdate",
                    Value = patient.BirthDate
                });

                if (patient.Telecom.Count() > 0)
                {
                    indices.Add(new ResourceIndex
                    {
                        ResourceType = "Patient",
                        ResourceId = patient.Id,
                        SearchParamCode = "telecom",
                        Value = patient.Telecom.FirstOrDefault().Value
                    });
                }

                //indices.Add(new ResourceIndex
                //{
                //    ResourceType = "Patient",
                //    ResourceId = patient.Id,
                //    SearchParamCode = "nationalId",
                //    Value = patient.Identifier
                //});

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
