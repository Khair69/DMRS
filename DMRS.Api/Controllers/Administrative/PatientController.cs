using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using DMRS.Api.Infrastructure.Search.Administrative;

namespace DMRS.Api.Controllers.Administrative
{
    public class PatientController : FhirBaseController<Patient>
    {
        public PatientController(IFhirRepository repository, ILogger<PatientController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, PatientIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
