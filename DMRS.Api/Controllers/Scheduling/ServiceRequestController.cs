using DMRS.Api.Application.Interfaces;
using DMRS.Api.Controllers.Administrative;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Scheduling;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Scheduling
{
    public class ServiceRequestController : FhirBaseController<ServiceRequest>
    {
        public ServiceRequestController(IFhirRepository repository, ILogger<ServiceRequestController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, ServiceRequestIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
