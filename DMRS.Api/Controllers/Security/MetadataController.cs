using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Security;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Security
{
    public class MetadataController : FhirBaseController<Provenance>
    {
        public MetadataController(IFhirRepository repository, ILogger<MetadataController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, MetadataIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, validator, searchIndexer, authorizationService)
        {
        }
    }
}
