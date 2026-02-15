using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace DMRS.Api.Controllers.Administrativ
{
    public class OrganizationController : FhirBaseController<Organization>
    {
        public OrganizationController(IFhirRepository repository, ILogger<OrganizationController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, OrganizationIndexer searchIndexer) : base(repository, logger, deserializer, validator, searchIndexer)
        {
        }
    }
}
