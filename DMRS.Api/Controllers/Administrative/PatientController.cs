using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Administrative
{
    public class PatientController : FhirBaseController<Patient>
    {
        public PatientController(IFhirRepository repository, ILogger<PatientController> logger, FhirJsonDeserializer deserializer, FhirJsonSerializer serializer, IFhirValidatorService validator, PatientIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, serializer, validator, searchIndexer, authorizationService)
        {
        }
    }
}
