using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Security
{
    public class MetadataIndexer : ISearchIndexer
    {
        private static void AddIndex(List<ResourceIndex> indices, string resourceId, string code, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            indices.Add(new ResourceIndex
            {
                ResourceType = "Provenance",
                ResourceId = resourceId,
                SearchParamCode = code,
                Value = value.ToLowerInvariant()
            });
        }

        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not Provenance provenance)
                return indices;

            AddIndex(indices, provenance.Id, "_id", provenance.Id);
            AddIndex(indices, provenance.Id, "_lastUpdated", provenance.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, provenance.Id, "recorded", provenance.RecordedElement?.Value?.ToString("o"));
            AddIndex(indices, provenance.Id, "location", provenance.Location?.Reference);

            AddIndex(indices, provenance.Id, "activity", provenance.Activity?.Text);
            foreach (var coding in provenance.Activity?.Coding ?? [])
                AddIndex(indices, provenance.Id, "activity", coding.Code);

            AddIndex(indices, provenance.Id, "when", provenance.Occurred is Period occurredPeriod ? occurredPeriod.Start : null);
            AddIndex(indices, provenance.Id, "when", provenance.Occurred is Period endOccurredPeriod ? endOccurredPeriod.End : null);
            AddIndex(indices, provenance.Id, "when", provenance.Occurred is FhirDateTime occurredDate ? occurredDate.Value : null);

            foreach (var target in provenance.Target)
            {
                AddIndex(indices, provenance.Id, "target", target.Reference);

                if (target.Reference is string targetRef && targetRef.StartsWith("patient/", StringComparison.OrdinalIgnoreCase))
                    AddIndex(indices, provenance.Id, "patient", targetRef);
            }

            foreach (var agent in provenance.Agent)
                AddIndex(indices, provenance.Id, "agent", agent.Who?.Reference);

            foreach (var entity in provenance.Entity)
                AddIndex(indices, provenance.Id, "entity", entity.What?.Reference);

            return indices;
        }
    }
}
