using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Clinical;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Clinical
{
    public class ProcedureController : FhirBaseController<Procedure>
    {
        public ProcedureController(IFhirRepository repository, ILogger<ProcedureController> logger, FhirJsonDeserializer deserializer, FhirJsonSerializer serializer, IFhirValidatorService validator, ProcedureIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, serializer, validator, searchIndexer, authorizationService)
        {
        }
    }
}
