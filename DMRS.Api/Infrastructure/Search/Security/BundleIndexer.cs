using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Security
{
    public class BundleIndexer : ResourceSearchIndexerBase
    {
        protected override string ResourceType => "Bundle";

        public override List<ResourceIndex> Extract(Resource resource)
        {
            var indices = new List<ResourceIndex>();

            if (resource is not Bundle bundle)
                return indices;

            AddIndex(indices, bundle.Id, "_id", bundle.Id);
            AddIndex(indices, bundle.Id, "_lastUpdated", bundle.Meta?.LastUpdated?.ToString("o"));
            AddIndex(indices, bundle.Id, "type", bundle.Type?.ToString());
            AddIndex(indices, bundle.Id, "timestamp", bundle.TimestampElement?.Value?.ToString("o"));
            AddIndex(indices, bundle.Id, "identifier", bundle.Identifier?.Value);
            AddIndex(indices, bundle.Id, "identifier", string.IsNullOrWhiteSpace(bundle.Identifier?.System) ? null : $"{bundle.Identifier.System}|{bundle.Identifier.Value}");

            foreach (var entry in bundle.Entry)
            {
                AddIndex(indices, bundle.Id, "fullurl", entry.FullUrl);

                if (entry.Resource is Composition composition)
                    AddIndex(indices, bundle.Id, "composition", $"composition/{composition.Id}");

                if (entry.Resource is MessageHeader messageHeader)
                    AddIndex(indices, bundle.Id, "message", $"messageheader/{messageHeader.Id}");
            }

            foreach (var link in bundle.Link)
                AddIndex(indices, bundle.Id, "link", link.Url);

            return indices;
        }
    }
}