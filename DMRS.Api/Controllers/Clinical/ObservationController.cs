using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Clinical;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Clinical
{
    public class ObservationController : FhirBaseController<Observation>
    {
        public ObservationController(IFhirRepository repository, ILogger<ObservationController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, ObservationIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
