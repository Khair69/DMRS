using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using Hl7.Fhir.Model;

namespace DMRS.Api.Infrastructure.Search.Medication
{
    public class MedicationRequestIndexer : ISearchIndexer
    {
        public List<ResourceIndex> Extract(Resource resource)
        {
            throw new NotImplementedException();
        }
    }
}
