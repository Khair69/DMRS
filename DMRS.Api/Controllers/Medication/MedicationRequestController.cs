using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Medication;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Medication
{
    public class MedicationRequestController : FhirBaseController<MedicationRequest>
    {
        public MedicationRequestController(IFhirRepository repository, ILogger<MedicationRequestController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, MedicationRequestIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, validator, searchIndexer, authorizationService)
        {
        }
    }
}
