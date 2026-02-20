using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Administrative
{
    public class PractitionerRoleIndexer : ResourceSearchIndexerBase
    {
        protected override string ResourceType => "PractitionerRole";

        public override List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is PractitionerRole practitionerRole)
            {
                AddIndex(indices, practitionerRole.Id, "_id", practitionerRole.Id);
                AddIndex(indices, practitionerRole.Id, "_lastUpdated", practitionerRole.Meta?.LastUpdated?.ToString("o"));
                AddIndex(indices, practitionerRole.Id, "active", practitionerRole.Active?.ToString());
                AddIndex(indices, practitionerRole.Id, "practitioner", practitionerRole.Practitioner?.Reference);
                AddIndex(indices, practitionerRole.Id, "organization", practitionerRole.Organization?.Reference);

                foreach (var identifier in practitionerRole.Identifier)
                {
                    AddIndex(indices, practitionerRole.Id, "identifier", identifier.Value);
                    AddIndex(indices, practitionerRole.Id, "identifier", string.IsNullOrWhiteSpace(identifier.System) ? null : $"{identifier.System}|{identifier.Value}");
                }

                foreach (var code in practitionerRole.Code)
                {
                    AddIndex(indices, practitionerRole.Id, "role", code.Text);
                    foreach (var coding in code.Coding)
                    {
                        AddIndex(indices, practitionerRole.Id, "role", coding.Code);
                    }
                }
            }

            return indices;
        }
    }
}
