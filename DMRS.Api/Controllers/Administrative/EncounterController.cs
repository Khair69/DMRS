using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Administrativ
{
    public class EncounterController : FhirBaseController<Encounter>
    {
        public EncounterController(IFhirRepository repository, ILogger<EncounterController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, EncounterIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
