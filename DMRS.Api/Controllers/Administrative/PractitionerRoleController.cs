using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Administrative
{
    public class PractitionerRoleController : FhirBaseController<PractitionerRole>
    {
        public PractitionerRoleController(IFhirRepository repository, ILogger<PractitionerRoleController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, PractitionerRoleIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, validator, searchIndexer, authorizationService)
        {
        }
    }
}
