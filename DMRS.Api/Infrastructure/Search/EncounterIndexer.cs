using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search
{
    public class EncounterIndexer : ISearchIndexer
    {
        private static void AddIndex(List<ResourceIndex> indices, string resourceId, string code, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            indices.Add(new ResourceIndex
            {
                ResourceType = "Encounter",
                ResourceId = resourceId,
                SearchParamCode = code,
                Value = value.ToLowerInvariant()
            });
        }

        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not Encounter encounter)
                return indices;

            AddIndex(indices, encounter.Id, "_id", encounter.Id);
            AddIndex(indices, encounter.Id, "_lastUpdated", encounter.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, encounter.Id, "status", encounter.Status?.ToString());
            AddIndex(indices, encounter.Id, "class", encounter.Class?.FirstOrDefault().Coding.FirstOrDefault().Code);
            AddIndex(indices, encounter.Id, "subject", encounter.Subject?.Reference);
            AddIndex(indices, encounter.Id, "patient", encounter.Subject?.Reference);
            AddIndex(indices, encounter.Id, "service-provider", encounter.ServiceProvider?.Reference);
            AddIndex(indices, encounter.Id, "part-of", encounter.PartOf?.Reference);

            AddIndex(indices, encounter.Id, "date", encounter.ActualPeriod?.Start);
            AddIndex(indices, encounter.Id, "date", encounter.ActualPeriod?.End);

            foreach (var identifier in encounter.Identifier)
            {
                AddIndex(indices, encounter.Id, "identifier", identifier.Value);
                AddIndex(indices, encounter.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
            }

            foreach (var type in encounter.Type)
            {
                AddIndex(indices, encounter.Id, "type", type.Text);

                foreach (var coding in type.Coding)
                    AddIndex(indices, encounter.Id, "type", coding.Code);
            }

            foreach (var appointment in encounter.Appointment)
                AddIndex(indices, encounter.Id, "appointment", appointment.Reference);

            foreach (var participant in encounter.Participant)
            {
                AddIndex(indices, encounter.Id, "participant", participant.Actor?.Reference);

                if (participant.Actor?.Reference is string actorRef && actorRef.StartsWith("practitioner/", StringComparison.OrdinalIgnoreCase))
                    AddIndex(indices, encounter.Id, "practitioner", actorRef);

                foreach (var participantType in participant.Type)
                {
                    AddIndex(indices, encounter.Id, "participant-type", participantType.Text);

                    foreach (var coding in participantType.Coding)
                        AddIndex(indices, encounter.Id, "participant-type", coding.Code);
                }
            }

            foreach (var reason in encounter.Reason)
            {
                foreach (var use in reason.Use)
                {
                    AddIndex(indices, encounter.Id, "reason-code", use.Text);

                    foreach (var coding in use.Coding)
                        AddIndex(indices, encounter.Id, "reason-code", coding.Code);
                }

                foreach (var reasonValue in reason.Value)
                {
                    AddIndex(indices, encounter.Id, "reason-code", reasonValue.Concept?.Text);

                    foreach (var coding in reasonValue.Concept?.Coding ?? [])
                        AddIndex(indices, encounter.Id, "reason-code", coding.Code);

                    AddIndex(indices, encounter.Id, "reason-reference", reasonValue.Reference?.Reference);
                }
            }

            foreach (var diagnosis in encounter.Diagnosis)
            {
                foreach (var condition in diagnosis.Condition)
                {
                    AddIndex(indices, encounter.Id, "diagnosis", condition.Reference?.Reference);
                    AddIndex(indices, encounter.Id, "diagnosis", condition.Concept?.Text);

                    foreach (var coding in condition.Concept?.Coding ?? [])
                        AddIndex(indices, encounter.Id, "diagnosis", coding.Code);
                }

                foreach (var use in diagnosis.Use)
                {
                    AddIndex(indices, encounter.Id, "diagnosis-use", use.Text);

                    foreach (var coding in use.Coding)
                        AddIndex(indices, encounter.Id, "diagnosis-use", coding.Code);
                }
            }

            foreach (var location in encounter.Location)
                AddIndex(indices, encounter.Id, "location", location.Location?.Reference);

            foreach (var episode in encounter.EpisodeOfCare)
                AddIndex(indices, encounter.Id, "episode-of-care", episode.Reference);

            return indices;
        }
    }
}
