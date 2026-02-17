using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Clinical
{
    public class ConditionIndexer : ResourceSearchIndexerBase
    {
        protected override string ResourceType => "Condition";

        public override List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not Condition condition)
                return indices;

            AddIndex(indices, condition.Id, "_id", condition.Id);
            AddIndex(indices, condition.Id, "_lastUpdated", condition.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, condition.Id, "subject", condition.Subject?.Reference);
            AddIndex(indices, condition.Id, "patient", condition.Subject?.Reference);
            AddIndex(indices, condition.Id, "encounter", condition.Encounter?.Reference);
            AddIndex(indices, condition.Id, "recorded-date", condition.RecordedDateElement?.Value);
            AddIndex(indices, condition.Id, "onset-date", condition.Onset is FhirDateTime onsetDate ? onsetDate.Value : null);
            AddIndex(indices, condition.Id, "abatement-date", condition.Abatement is FhirDateTime abatementDate ? abatementDate.Value : null);

            foreach (var identifier in condition.Identifier)
            {
                AddIndex(indices, condition.Id, "identifier", identifier.Value);
                AddIndex(indices, condition.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
            }

            AddIndex(indices, condition.Id, "code", condition.Code?.Text);
            foreach (var coding in condition.Code?.Coding ?? [])
                AddIndex(indices, condition.Id, "code", coding.Code);

            foreach (var coding in condition.ClinicalStatus?.Coding ?? [])
                AddIndex(indices, condition.Id, "clinical-status", coding.Code);

            AddIndex(indices, condition.Id, "clinical-status", condition.ClinicalStatus?.Text);

            foreach (var coding in condition.VerificationStatus?.Coding ?? [])
                AddIndex(indices, condition.Id, "verification-status", coding.Code);

            AddIndex(indices, condition.Id, "verification-status", condition.VerificationStatus?.Text);

            AddIndex(indices, condition.Id, "severity", condition.Severity?.Text);
            foreach (var coding in condition.Severity?.Coding ?? [])
                AddIndex(indices, condition.Id, "severity", coding.Code);

            AddIndex(indices, condition.Id, "asserter", condition.Participant.FirstOrDefault().Actor.Reference);

            foreach (var category in condition.Category)
            {
                AddIndex(indices, condition.Id, "category", category.Text);
                foreach (var coding in category.Coding)
                    AddIndex(indices, condition.Id, "category", coding.Code);
            }

            foreach (var bodySite in condition.BodySite)
            {
                AddIndex(indices, condition.Id, "body-site", bodySite.Text);
                foreach (var coding in bodySite.Coding)
                    AddIndex(indices, condition.Id, "body-site", coding.Code);
            }

            return indices;
        }
    }
}
