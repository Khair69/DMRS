using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers
{
    public class PractitionerController : FhirBaseController<Practitioner>
    {
        public PractitionerController(IFhirRepository repository, ILogger<PractitionerController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator) : base(repository, logger, deserializer, validator)
        {
        }
    }
}
