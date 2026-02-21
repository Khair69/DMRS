using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Administrative
{
    public class LocationController : FhirBaseController<Location>
    {
        public LocationController(IFhirRepository repository, ILogger<LocationController> logger, FhirJsonDeserializer deserializer, FhirJsonSerializer serializer, IFhirValidatorService validator, LocationIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, serializer, validator, searchIndexer, authorizationService)
        {
        }
    }
}
