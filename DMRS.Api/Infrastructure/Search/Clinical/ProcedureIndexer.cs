using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Clinical
{
    public class ProcedureIndexer : ResourceSearchIndexerBase
    {
        protected override string ResourceType => "Procedure";

        public override List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not Procedure procedure)
                return indices;

            AddIndex(indices, procedure.Id, "_id", procedure.Id);
            AddIndex(indices, procedure.Id, "_lastUpdated", procedure.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, procedure.Id, "status", procedure.Status?.ToString());
            AddIndex(indices, procedure.Id, "subject", procedure.Subject?.Reference);
            AddIndex(indices, procedure.Id, "patient", procedure.Subject?.Reference);
            AddIndex(indices, procedure.Id, "encounter", procedure.Encounter?.Reference);
            AddIndex(indices, procedure.Id, "date", procedure.Occurrence is FhirDateTime dateTime ? dateTime.Value : null);
            AddIndex(indices, procedure.Id, "date", procedure.Occurrence is Period period ? period.Start : null);
            AddIndex(indices, procedure.Id, "date", procedure.Occurrence is Period datePeriod ? datePeriod.End : null);
            AddIndex(indices, procedure.Id, "location", procedure.Location?.Reference);

            foreach (var identifier in procedure.Identifier)
            {
                AddIndex(indices, procedure.Id, "identifier", identifier.Value);
                AddIndex(indices, procedure.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
            }

            AddIndex(indices, procedure.Id, "code", procedure.Code?.Text);
            foreach (var coding in procedure.Code?.Coding ?? [])
                AddIndex(indices, procedure.Id, "code", coding.Code);

            foreach (var category in procedure.Category ?? [])
                AddIndex(indices, procedure.Id, "category", category.Coding.FirstOrDefault().Code);

            AddIndex(indices, procedure.Id, "category", procedure.Category.FirstOrDefault().Text);

            foreach (var basedOn in procedure.BasedOn)
                AddIndex(indices, procedure.Id, "based-on", basedOn.Reference);

            foreach (var partOf in procedure.PartOf)
                AddIndex(indices, procedure.Id, "part-of", partOf.Reference);

            foreach (var performer in procedure.Performer)
                AddIndex(indices, procedure.Id, "performer", performer.Actor?.Reference);

            foreach (var reason in procedure.Reason)
            {
                AddIndex(indices, procedure.Id, "reason-reference", reason.Reference?.Reference);
                AddIndex(indices, procedure.Id, "reason-code", reason.Concept?.Text);

                foreach (var coding in reason.Concept?.Coding ?? [])
                    AddIndex(indices, procedure.Id, "reason-code", coding.Code);
            }

            return indices;
        }
    }
}
