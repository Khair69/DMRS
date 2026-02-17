using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Administrative
{
    public class OrganizationIndexer : ResourceSearchIndexerBase
    {
        protected override string ResourceType => "Organization";
        public override List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is Organization organization)
            {
                AddIndex(indices, organization.Id, "_id", organization.Id);
                AddIndex(indices, organization.Id, "_lastUpdated", organization.Meta?.LastUpdated?.ToString("o"));
                AddIndex(indices, organization.Id, "active", organization.Active?.ToString());
                AddIndex(indices, organization.Id, "name", organization.Name);

                foreach (var alias in organization.Alias)
                    AddIndex(indices, organization.Id, "name", alias);

                foreach (var identifier in organization.Identifier)
                {
                    AddIndex(indices, organization.Id, "identifier", identifier.Value);
                    AddIndex(indices, organization.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
                }

                foreach (var type in organization.Type)
                {
                    AddIndex(indices, organization.Id, "type", type.Text);
                    foreach (var coding in type.Coding)
                        AddIndex(indices, organization.Id, "type", coding.Code);
                }

                AddIndex(indices, organization.Id, "partof", organization.PartOf?.Reference);

                foreach (var endpoint in organization.Endpoint)
                    AddIndex(indices, organization.Id, "endpoint", endpoint.Reference);

                foreach (var contact in organization.Contact)
                {
                    foreach (var telecom in contact.Telecom)
                    {
                        AddIndex(indices, organization.Id, "telecom", telecom.Value);
                        if (telecom.System == ContactPoint.ContactPointSystem.Phone)
                            AddIndex(indices, organization.Id, "phone", telecom.Value);
                        if (telecom.System == ContactPoint.ContactPointSystem.Email)
                            AddIndex(indices, organization.Id, "email", telecom.Value);
                    }
                }

                foreach (var contact in organization.Contact)
                {
                    AddIndex(indices, organization.Id, "address", contact.Address.Text);
                    AddIndex(indices, organization.Id, "address-city", contact.Address.City);
                    AddIndex(indices, organization.Id, "address-state", contact.Address.State);
                    AddIndex(indices, organization.Id, "address-postalcode", contact.Address.PostalCode);
                    AddIndex(indices, organization.Id, "address-country", contact.Address.Country);
                }
            }
            return indices;

        }
    }
}
