using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Clinical
{
    public class AllergyIntoleranceIndexer : ISearchIndexer
    {
        private static void AddIndex(List<ResourceIndex> indices, string resourceId, string code, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            indices.Add(new ResourceIndex
            {
                ResourceType = "AllergyIntolerance",
                ResourceId = resourceId,
                SearchParamCode = code,
                Value = value.ToLowerInvariant()
            });
        }

        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not AllergyIntolerance allergy)
                return indices;

            AddIndex(indices, allergy.Id, "_id", allergy.Id);
            AddIndex(indices, allergy.Id, "_lastUpdated", allergy.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, allergy.Id, "clinical-status", allergy.ClinicalStatus?.Text);
            AddIndex(indices, allergy.Id, "verification-status", allergy.VerificationStatus?.Text);
            AddIndex(indices, allergy.Id, "type", allergy.Type?.ToString());
            AddIndex(indices, allergy.Id, "criticality", allergy.Criticality?.ToString());
            AddIndex(indices, allergy.Id, "patient", allergy.Patient?.Reference);
            AddIndex(indices, allergy.Id, "encounter", allergy.Encounter?.Reference);
            AddIndex(indices, allergy.Id, "recorder", allergy.Recorder?.Reference);
            AddIndex(indices, allergy.Id, "asserter", allergy.Asserter?.Reference);
            AddIndex(indices, allergy.Id, "onset", allergy.Onset is FhirDateTime onsetDate ? onsetDate.Value : null);
            AddIndex(indices, allergy.Id, "last-date", allergy.LastOccurrenceElement?.Value);

            foreach (var category in allergy.Category)
                AddIndex(indices, allergy.Id, "category", category.ToString());

            foreach (var coding in allergy.ClinicalStatus?.Coding ?? [])
                AddIndex(indices, allergy.Id, "clinical-status", coding.Code);

            foreach (var coding in allergy.VerificationStatus?.Coding ?? [])
                AddIndex(indices, allergy.Id, "verification-status", coding.Code);

            AddIndex(indices, allergy.Id, "code", allergy.Code?.Text);
            foreach (var coding in allergy.Code?.Coding ?? [])
                AddIndex(indices, allergy.Id, "code", coding.Code);

            foreach (var reaction in allergy.Reaction)
            {
                AddIndex(indices, allergy.Id, "manifestation", reaction.Description);
                AddIndex(indices, allergy.Id, "severity", reaction.Severity?.ToString());

                foreach (var manifestation in reaction.Manifestation)
                {
                    AddIndex(indices, allergy.Id, "manifestation", manifestation.Text);
                    foreach (var coding in manifestation.Coding)
                        AddIndex(indices, allergy.Id, "manifestation", coding.Code);
                }
            }

            return indices;
        }
    }
}
