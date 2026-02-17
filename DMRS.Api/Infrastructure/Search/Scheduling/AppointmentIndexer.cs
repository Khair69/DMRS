using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Scheduling
{
    public class AppointmentIndexer : ISearchIndexer
    {
        private static void AddIndex(List<ResourceIndex> indices, string resourceId, string code, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            indices.Add(new ResourceIndex
            {
                ResourceType = "Appointment",
                ResourceId = resourceId,
                SearchParamCode = code,
                Value = value.ToLowerInvariant()
            });
        }

        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not Appointment appointment)
                return indices;

            AddIndex(indices, appointment.Id, "_id", appointment.Id);
            AddIndex(indices, appointment.Id, "_lastUpdated", appointment.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, appointment.Id, "status", appointment.Status?.ToString());
            AddIndex(indices, appointment.Id, "date", appointment.StartElement?.Value?.ToString("o"));
            AddIndex(indices, appointment.Id, "date", appointment.EndElement?.Value?.ToString("o"));
            AddIndex(indices, appointment.Id, "appointment-type", appointment.AppointmentType?.Text);

            foreach (var identifier in appointment.Identifier)
            {
                AddIndex(indices, appointment.Id, "identifier", identifier.Value);
                AddIndex(indices, appointment.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
            }

            foreach (var coding in appointment.AppointmentType?.Coding ?? [])
                AddIndex(indices, appointment.Id, "appointment-type", coding.Code);

            foreach (var serviceType in appointment.ServiceType)
            {
                AddIndex(indices, appointment.Id, "service-type", serviceType.Text);
                foreach (var coding in serviceType.Coding)
                    AddIndex(indices, appointment.Id, "service-type", coding.Code);
            }

            foreach (var specialty in appointment.Specialty)
            {
                AddIndex(indices, appointment.Id, "specialty", specialty.Text);
                foreach (var coding in specialty.Coding)
                    AddIndex(indices, appointment.Id, "specialty", coding.Code);
            }

            foreach (var basedOn in appointment.BasedOn)
                AddIndex(indices, appointment.Id, "based-on", basedOn.Reference);

            foreach (var slot in appointment.Slot)
                AddIndex(indices, appointment.Id, "slot", slot.Reference);

            foreach (var supportingInformation in appointment.SupportingInformation)
                AddIndex(indices, appointment.Id, "supporting-info", supportingInformation.Reference);

            foreach (var reason in appointment.Reason)
            {
                AddIndex(indices, appointment.Id, "reason-code", reason.Concept?.Text);
                AddIndex(indices, appointment.Id, "reason-reference", reason.Reference?.Reference);

                foreach (var coding in reason.Concept?.Coding ?? [])
                    AddIndex(indices, appointment.Id, "reason-code", coding.Code);
            }

            foreach (var participant in appointment.Participant)
            {
                AddIndex(indices, appointment.Id, "actor", participant.Actor?.Reference);

                if (participant.Actor?.Reference is not string actorReference)
                    continue;

                if (actorReference.StartsWith("patient/", StringComparison.OrdinalIgnoreCase))
                    AddIndex(indices, appointment.Id, "patient", actorReference);

                if (actorReference.StartsWith("practitioner/", StringComparison.OrdinalIgnoreCase))
                    AddIndex(indices, appointment.Id, "practitioner", actorReference);

                if (actorReference.StartsWith("location/", StringComparison.OrdinalIgnoreCase))
                    AddIndex(indices, appointment.Id, "location", actorReference);
            }

            return indices;
        }
    }
}
