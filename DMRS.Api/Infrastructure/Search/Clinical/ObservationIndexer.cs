using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Clinical
{
    public class ObservationIndexer : ISearchIndexer
    {
        private static void AddIndex(List<ResourceIndex> indices, string resourceId, string code, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            indices.Add(new ResourceIndex
            {
                ResourceType = "Observation",
                ResourceId = resourceId,
                SearchParamCode = code,
                Value = value.ToLowerInvariant()
            });
        }

        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not Observation observation)
                return indices;

            AddIndex(indices, observation.Id, "_id", observation.Id);
            AddIndex(indices, observation.Id, "_lastUpdated", observation.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, observation.Id, "status", observation.Status?.ToString());
            AddIndex(indices, observation.Id, "subject", observation.Subject?.Reference);
            AddIndex(indices, observation.Id, "patient", observation.Subject?.Reference);
            AddIndex(indices, observation.Id, "encounter", observation.Encounter?.Reference);
            AddIndex(indices, observation.Id, "date", observation.Effective is FhirDateTime dateTime ? dateTime.Value : null);
            AddIndex(indices, observation.Id, "date", observation.Effective is Period period ? period.Start : null);
            AddIndex(indices, observation.Id, "date", observation.Effective is Period datePeriod ? datePeriod.End : null);
            AddIndex(indices, observation.Id, "date", observation.IssuedElement?.Value?.ToString("o"));
            AddIndex(indices, observation.Id, "device", observation.Device?.Reference);
            AddIndex(indices, observation.Id, "specimen", observation.Specimen?.Reference);

            foreach (var identifier in observation.Identifier)
            {
                AddIndex(indices, observation.Id, "identifier", identifier.Value);
                AddIndex(indices, observation.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
            }

            AddIndex(indices, observation.Id, "code", observation.Code?.Text);
            foreach (var coding in observation.Code?.Coding ?? [])
                AddIndex(indices, observation.Id, "code", coding.Code);

            foreach (var category in observation.Category)
            {
                AddIndex(indices, observation.Id, "category", category.Text);
                foreach (var coding in category.Coding)
                    AddIndex(indices, observation.Id, "category", coding.Code);
            }

            foreach (var basedOn in observation.BasedOn)
                AddIndex(indices, observation.Id, "based-on", basedOn.Reference);

            foreach (var performer in observation.Performer)
                AddIndex(indices, observation.Id, "performer", performer.Reference);

            foreach (var focus in observation.Focus)
                AddIndex(indices, observation.Id, "focus", focus.Reference);

            if (observation.Value is Quantity quantity)
            {
                AddIndex(indices, observation.Id, "value-quantity", quantity.Value?.ToString());
                AddIndex(indices, observation.Id, "value-quantity", quantity.Code);
                AddIndex(indices, observation.Id, "value-quantity", string.IsNullOrWhiteSpace(quantity.System) || string.IsNullOrWhiteSpace(quantity.Code)
                    ? null
                    : $"{quantity.System}|{quantity.Code}");
            }

            return indices;
        }
    }
}
