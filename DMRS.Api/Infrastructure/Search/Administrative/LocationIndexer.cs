using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Administrative
{
    public class LocationIndexer : ISearchIndexer
    {
        private static void AddIndex(List<ResourceIndex> indices, string resourceId, string code, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            indices.Add(new ResourceIndex
            {
                ResourceType = "Location",
                ResourceId = resourceId,
                SearchParamCode = code,
                Value = value.ToLowerInvariant()
            });
        }

        public List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not Location location)
                return indices;

            AddIndex(indices, location.Id, "_id", location.Id);
            AddIndex(indices, location.Id, "_lastUpdated", location.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, location.Id, "name", location.Name);
            AddIndex(indices, location.Id, "status", location.Status?.ToString());
            AddIndex(indices, location.Id, "operational-status", location.OperationalStatus?.Code);
            AddIndex(indices, location.Id, "partof", location.PartOf?.Reference);
            AddIndex(indices, location.Id, "organization", location.ManagingOrganization?.Reference);

            foreach (var identifier in location.Identifier)
            {
                AddIndex(indices, location.Id, "identifier", identifier.Value);
                AddIndex(indices, location.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
            }

            foreach (var type in location.Type)
            {
                AddIndex(indices, location.Id, "type", type.Text);

                foreach (var coding in type.Coding)
                    AddIndex(indices, location.Id, "type", coding.Code);
            }

            foreach (var contact in location.Contact)
                foreach (var telecom in contact.Telecom)
                AddIndex(indices, location.Id, "telecom", telecom.Value);

            foreach (var endpoint in location.Endpoint)
                AddIndex(indices, location.Id, "endpoint", endpoint.Reference);

            if (location.Address is not null)
            {
                AddIndex(indices, location.Id, "address", location.Address.Text);
                AddIndex(indices, location.Id, "address-city", location.Address.City);
                AddIndex(indices, location.Id, "address-state", location.Address.State);
                AddIndex(indices, location.Id, "address-postalcode", location.Address.PostalCode);
                AddIndex(indices, location.Id, "address-country", location.Address.Country);
                AddIndex(indices, location.Id, "address-use", location.Address.Use.ToString());
            }

            return indices;
        }
    }
}
