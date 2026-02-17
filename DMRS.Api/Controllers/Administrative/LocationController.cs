using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Administrative
{
    public class LocationController : FhirBaseController<Location>
    {
        public LocationController(IFhirRepository repository, ILogger<LocationController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, LocationIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
