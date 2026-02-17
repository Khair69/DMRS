using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Clinical;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Clinical
{
    public class ProcedureController : FhirBaseController<Procedure>
    {
        public ProcedureController(IFhirRepository repository, ILogger<ProcedureController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, ProcedureIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
