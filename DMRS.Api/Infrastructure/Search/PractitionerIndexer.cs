using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search
{
    public class PractitionerIndexer : ISearchIndexer
    {
        private static void AddIndex(List<ResourceIndex> indices, string resourceId, string code, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            indices.Add(new ResourceIndex
            {
                ResourceType = "Practitioner",
                ResourceId = resourceId,
                SearchParamCode = code,
                Value = value.ToLowerInvariant()
            });
        }

        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is Practitioner practitioner)
            {
                AddIndex(indices, practitioner.Id, "_id", practitioner.Id);
                AddIndex(indices, practitioner.Id, "_lastUpdated", practitioner.Meta?.LastUpdated?.ToString("o"));
                AddIndex(indices, practitioner.Id, "active", practitioner.Active?.ToString());

                if (practitioner.Gender.HasValue)
                    AddIndex(indices, practitioner.Id, "gender", practitioner.Gender.Value.ToString());

                foreach (var identifier in practitioner.Identifier)
                {
                    AddIndex(indices, practitioner.Id, "identifier", identifier.Value);
                    AddIndex(indices, practitioner.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
                }

                foreach (var telecom in practitioner.Telecom)
                {
                    AddIndex(indices, practitioner.Id, "telecom", telecom.Value);

                    if (telecom.System == ContactPoint.ContactPointSystem.Phone)
                        AddIndex(indices, practitioner.Id, "phone", telecom.Value);

                    if (telecom.System == ContactPoint.ContactPointSystem.Email)
                        AddIndex(indices, practitioner.Id, "email", telecom.Value);
                }

                foreach (var address in practitioner.Address)
                {
                    AddIndex(indices, practitioner.Id, "address", address.Text);
                    AddIndex(indices, practitioner.Id, "address-city", address.City);
                    AddIndex(indices, practitioner.Id, "address-state", address.State);
                    AddIndex(indices, practitioner.Id, "address-postalcode", address.PostalCode);
                    AddIndex(indices, practitioner.Id, "address-country", address.Country);
                }

                foreach (var name in practitioner.Name)
                {
                    AddIndex(indices, practitioner.Id, "name", name.Text);
                    AddIndex(indices, practitioner.Id, "family", name.Family);

                    foreach (var given in name.Given)
                        AddIndex(indices, practitioner.Id, "given", given);
                }

                foreach (var communication in practitioner.Communication)
                {
                    AddIndex(indices, practitioner.Id, "communication", communication.Language.Text);

                    foreach (var coding in communication.Language.Coding)
                        AddIndex(indices, practitioner.Id, "communication", coding.Code);
                }
            }

            return indices;
        }
    }
}
