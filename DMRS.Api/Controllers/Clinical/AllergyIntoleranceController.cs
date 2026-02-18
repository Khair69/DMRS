using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Clinical;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Clinical
{
    public class AllergyIntoleranceController : FhirBaseController<AllergyIntolerance>
    {
        public AllergyIntoleranceController(IFhirRepository repository, ILogger<AllergyIntoleranceController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, AllergyIntoleranceIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, validator, searchIndexer, authorizationService)
        {
        }
    }
}
