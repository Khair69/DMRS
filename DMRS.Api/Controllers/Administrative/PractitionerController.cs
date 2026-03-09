using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Administrative
{
    public class PractitionerController : FhirBaseController<Practitioner>
    {
        public PractitionerController(IFhirRepository repository, ILogger<PractitionerController> logger, FhirJsonDeserializer deserializer, FhirJsonSerializer serializer, IFhirValidatorService validator, PractitionerIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, serializer, validator, searchIndexer, authorizationService)
        {
        }

        [HttpDelete("{id}")]
        public override async Task<IActionResult> Delete(string id)
        {
            var roles = await _repository.SearchAsync<PractitionerRole>(new Dictionary<string, string>
            {
                ["practitioner"] = $"Practitioner/{id}"
            });

            if (roles.Count > 0)
            {
                return Conflict("Cannot delete Practitioner while PractitionerRole references exist. Reassign or delete roles first.");
            }

            return await base.Delete(id);
        }
    }
}
