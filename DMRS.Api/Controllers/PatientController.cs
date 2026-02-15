using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers
{
    public class PatientController : FhirBaseController<Patient>
    {
        public PatientController(IFhirRepository repository, ILogger<PatientController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator) : base(repository, logger, deserializer, validator)
        {
        }
    }
}
