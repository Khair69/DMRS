using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Administrative
{
    public class PatientController : FhirBaseController<Patient>
    {
        // Name params the autocomplete is allowed to suggest over — same string-type params the
        // Patients search box exposes. Anything else returns no suggestions.
        private static readonly HashSet<string> SuggestableFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "family", "given", "name",
        };

        public PatientController(IFhirRepository repository, ILogger<PatientController> logger, FhirJsonDeserializer deserializer, FhirJsonSerializer serializer, IFhirValidatorService validator, PatientIndexer searchIndexer, ISmartAuthorizationService authorizationService) : base(repository, logger, deserializer, serializer, validator, searchIndexer, authorizationService)
        {
        }

        // Type-ahead suggestions for the Patients search box. Returns a plain JSON string array, scoped
        // to the patients the caller may read so names never leak across organizations.
        [HttpGet("_suggest")]
        public async Task<IActionResult> Suggest([FromQuery] string field, [FromQuery] string value)
        {
            if (string.IsNullOrWhiteSpace(field) || !SuggestableFields.Contains(field) || string.IsNullOrWhiteSpace(value))
            {
                return Ok(Array.Empty<string>());
            }

            var accessiblePatientIds = await _authorizationService.ResolveAccessiblePatientIdsAsync(User);
            var suggestions = await _repository.SuggestValuesAsync<Patient>(field, value.Trim(), 10, accessiblePatientIds);
            return Ok(suggestions);
        }
    }
}
