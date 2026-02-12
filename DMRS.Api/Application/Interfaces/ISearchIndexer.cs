using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Application.Interfaces
{
    public interface ISearchIndexer
    {
        List<ResourceIndex> Extract(Resource resource);
    }
}
