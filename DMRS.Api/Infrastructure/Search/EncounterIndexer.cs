using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search
{
    public class EncounterIndexer : ISearchIndexer
    {
        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is Encounter encounter)
            {
                indices.Add(new ResourceIndex
                {
                    ResourceType = "Encounter",
                    ResourceId = encounter.Id,
                    SearchParamCode = "_id",
                    Value = encounter.Id
                });


                indices.Add(new ResourceIndex
                {
                    ResourceType = "Encounter",
                    ResourceId = encounter.Id,
                    SearchParamCode = "_lastUpdated",
                    Value = encounter.Meta.LastUpdated?.ToString("o") ?? string.Empty
                });

                indices.Add(new ResourceIndex
                {
                    ResourceType = "Encounter",
                    ResourceId = encounter.Id,
                    SearchParamCode = "patient",
                    Value = encounter.Subject.Reference
                });

                indices.Add(new ResourceIndex
                {
                    ResourceType = "Encounter",
                    ResourceId = encounter.Id,
                    SearchParamCode = "status",
                    Value = encounter.Status.ToString()
                });

                indices.Add(new ResourceIndex
                {
                    ResourceType = "Encounter",
                    ResourceId = encounter.Id,
                    SearchParamCode = "date",
                    Value = encounter.ActualPeriod.Start
                });
            }
            return indices;
        }
    }
}
