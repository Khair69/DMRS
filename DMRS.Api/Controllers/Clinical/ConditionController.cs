using DMRS.Api.Application.Interfaces;
using DMRS.Api.Controllers.Administrative;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Clinical
{
    public class ConditionController : FhirBaseController<Condition>
    {
        public ConditionController(IFhirRepository repository, ILogger<ConditionController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, PatientIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
