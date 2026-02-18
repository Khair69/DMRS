using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using DMRS.Api.Infrastructure.Security;

namespace DMRS.Api.Controllers.Administrativ
{
    public class EncounterController : FhirBaseController<Encounter>
    {
        public EncounterController(IFhirRepository repository, ILogger<EncounterController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, EncounterIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, validator, searchIndexer, authorizationService)
        {
        }
    }
}
