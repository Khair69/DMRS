using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Security
{
    public class BundleController : FhirBaseController<Bundle>
    {
        public BundleController(IFhirRepository repository, ILogger<BundleController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, BundleIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
