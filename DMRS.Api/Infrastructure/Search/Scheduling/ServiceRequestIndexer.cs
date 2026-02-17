using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Scheduling
{
    public class ServiceRequestIndexer : ResourceSearchIndexerBase
    {
        protected override string ResourceType => "ServiceRequest";

        public override List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not ServiceRequest serviceRequest)
                return indices;

            AddIndex(indices, serviceRequest.Id, "_id", serviceRequest.Id);
            AddIndex(indices, serviceRequest.Id, "_lastUpdated", serviceRequest.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, serviceRequest.Id, "status", serviceRequest.Status?.ToString());
            AddIndex(indices, serviceRequest.Id, "intent", serviceRequest.Intent?.ToString());
            AddIndex(indices, serviceRequest.Id, "priority", serviceRequest.Priority?.ToString());
            AddIndex(indices, serviceRequest.Id, "subject", serviceRequest.Subject?.Reference);
            AddIndex(indices, serviceRequest.Id, "patient", serviceRequest.Subject?.Reference);
            AddIndex(indices, serviceRequest.Id, "encounter", serviceRequest.Encounter?.Reference);
            AddIndex(indices, serviceRequest.Id, "authored", serviceRequest.AuthoredOnElement?.Value);
            AddIndex(indices, serviceRequest.Id, "requester", serviceRequest.Requester?.Reference);

            foreach (var identifier in serviceRequest.Identifier)
            {
                AddIndex(indices, serviceRequest.Id, "identifier", identifier.Value);
                AddIndex(indices, serviceRequest.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
            }

            AddIndex(indices, serviceRequest.Id, "requisition", serviceRequest.Requisition?.Value);

            AddIndex(indices, serviceRequest.Id, "code", serviceRequest.Code.Concept.Text);
            foreach (var coding in serviceRequest.Code?.Concept.Coding ?? [])
                AddIndex(indices, serviceRequest.Id, "code", coding.Code);

            foreach (var category in serviceRequest.Category)
            {
                AddIndex(indices, serviceRequest.Id, "category", category.Text);
                foreach (var coding in category.Coding)
                    AddIndex(indices, serviceRequest.Id, "category", coding.Code);
            }

            foreach (var performer in serviceRequest.Performer)
                AddIndex(indices, serviceRequest.Id, "performer", performer.Reference);

            foreach (var basedOn in serviceRequest.BasedOn)
                AddIndex(indices, serviceRequest.Id, "based-on", basedOn.Reference);

            foreach (var replaces in serviceRequest.Replaces)
                AddIndex(indices, serviceRequest.Id, "replaces", replaces.Reference);

            foreach (var specimen in serviceRequest.Specimen)
                AddIndex(indices, serviceRequest.Id, "specimen", specimen.Reference);

            foreach (var bodySite in serviceRequest.BodySite)
            {
                AddIndex(indices, serviceRequest.Id, "body-site", bodySite.Text);
                foreach (var coding in bodySite.Coding)
                    AddIndex(indices, serviceRequest.Id, "body-site", coding.Code);
            }

            AddIndex(indices, serviceRequest.Id, "occurrence", serviceRequest.Occurrence is FhirDateTime occurrenceDate ? occurrenceDate.Value : null);
            AddIndex(indices, serviceRequest.Id, "occurrence", serviceRequest.Occurrence is Period occurrencePeriod ? occurrencePeriod.Start : null);
            AddIndex(indices, serviceRequest.Id, "occurrence", serviceRequest.Occurrence is Period rangePeriod ? rangePeriod.End : null);

            return indices;
        }
    }
}
