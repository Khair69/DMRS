using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using DMRS.Api.Infrastructure.Search;

namespace DMRS.Api.Controllers.Administrativ
{
    public class PractitionerController : FhirBaseController<Practitioner>
    {
        public PractitionerController(IFhirRepository repository, ILogger<PractitionerController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, PractitionerIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
