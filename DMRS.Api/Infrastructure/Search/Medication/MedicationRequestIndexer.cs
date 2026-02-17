using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Medication
{
    public class MedicationRequestIndexer : ResourceSearchIndexerBase
    {
        protected override string ResourceType => "MedicationRequest";

        public override List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not MedicationRequest medicationRequest)
                return indices;

            AddIndex(indices, medicationRequest.Id, "_id", medicationRequest.Id);
            AddIndex(indices, medicationRequest.Id, "_lastUpdated", medicationRequest.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, medicationRequest.Id, "status", medicationRequest.Status?.ToString());
            AddIndex(indices, medicationRequest.Id, "intent", medicationRequest.Intent?.ToString());
            AddIndex(indices, medicationRequest.Id, "priority", medicationRequest.Priority?.ToString());
            AddIndex(indices, medicationRequest.Id, "subject", medicationRequest.Subject?.Reference);
            AddIndex(indices, medicationRequest.Id, "patient", medicationRequest.Subject?.Reference);
            AddIndex(indices, medicationRequest.Id, "encounter", medicationRequest.Encounter?.Reference);
            AddIndex(indices, medicationRequest.Id, "authoredon", medicationRequest.AuthoredOnElement?.Value);
            AddIndex(indices, medicationRequest.Id, "requester", medicationRequest.Requester?.Reference);
            AddIndex(indices, medicationRequest.Id, "performer", medicationRequest.Performer.FirstOrDefault().Reference);

            foreach (var identifier in medicationRequest.Identifier)
            {
                AddIndex(indices, medicationRequest.Id, "identifier", identifier.Value);
                AddIndex(indices, medicationRequest.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
            }

            AddIndex(indices, medicationRequest.Id, "group-or-identifier", medicationRequest.GroupIdentifier?.Value);

            AddIndex(indices, medicationRequest.Id, "medication", medicationRequest.Medication?.Reference?.Reference);
            AddIndex(indices, medicationRequest.Id, "code", medicationRequest.Medication?.Concept?.Text);

            foreach (var coding in medicationRequest.Medication?.Concept?.Coding ?? [])
                AddIndex(indices, medicationRequest.Id, "code", coding.Code);

            foreach (var category in medicationRequest.Category)
            {
                AddIndex(indices, medicationRequest.Id, "category", category.Text);
                foreach (var coding in category.Coding)
                    AddIndex(indices, medicationRequest.Id, "category", coding.Code);
            }

            return indices;
        }
    }
}
