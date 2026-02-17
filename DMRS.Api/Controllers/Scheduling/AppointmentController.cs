using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Scheduling;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Scheduling
{
    public class AppointmentController : FhirBaseController<Appointment>
    {
        public AppointmentController(IFhirRepository repository, ILogger<AppointmentController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, AppointmentIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
