using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    [ApiController]
    [Route("fhir/[controller]")]
    [Authorize(Policy = "FhirScope")]
    public abstract class FhirBaseController<T> : ControllerBase where T : Resource
    {
        protected readonly IFhirRepository _repository;
        protected readonly ILogger _logger;
        protected readonly FhirJsonDeserializer _deserializer;
        protected readonly FhirJsonSerializer _serializer;
        protected readonly IFhirValidatorService _validator;
        protected readonly ISearchIndexer _searchIndexer;
        protected readonly ISmartAuthorizationService _authorizationService;

        public FhirBaseController(
            IFhirRepository repository,
            ILogger logger,
            FhirJsonDeserializer deserializer,
            FhirJsonSerializer serializer,
            IFhirValidatorService validator,
            ISearchIndexer searchIndexer,
            ISmartAuthorizationService authorizationService)
        {
            _repository = repository;
            _logger = logger;
            _deserializer = deserializer;
            _serializer = serializer;
            _validator = validator;
            _searchIndexer = searchIndexer;
            _authorizationService = authorizationService;
        }

        [HttpGet("{id}")]
        public virtual async Task<IActionResult> Read(string id)
        {
            var resource = await _repository.GetAsync<T>(id);
            if (resource == null) return NotFound();

            if (!await CanAccessResource(resource, "read"))
            {
                return Forbid();
            }

            var jsonString = _serializer.SerializeToString(resource);
            return Content(jsonString, "application/fhir+json");
        }

        [HttpGet("{id}/_history/{vid}")]
        public virtual async Task<IActionResult> VRead(string id, string vid)
        {
            if (!int.TryParse(vid, out var versionId) || versionId <= 0)
            {
                return BadRequest("Invalid version id.");
            }

            var resource = await _repository.GetVersionAsync<T>(id, versionId);
            if (resource == null)
            {
                return NotFound();
            }

            if (!await CanAccessResource(resource, "read"))
            {
                return Forbid();
            }

            var jsonString = _serializer.SerializeToString(resource);
            return Content(jsonString, "application/fhir+json");
        }


        [HttpGet("search/{searchParam}/{value}")]
        public virtual async Task<IActionResult> Search(string searchParam, string value)
        {
            var resources = await _repository.SearchAsync<T>(searchParam, value);

            var filteredResources = new List<T>();
            foreach (var resource in resources)
            {
                if (await CanAccessResource(resource, "read"))
                {
                    filteredResources.Add(resource);
                }
            }
            if (filteredResources.Count == 0) return NotFound();

            return Ok(filteredResources);
        }

        [HttpPost]
        public virtual async Task<IActionResult> Create([FromBody] System.Text.Json.JsonElement body)
        {
            string jsonString = body.GetRawText();
            T resource;

            try
            {
                resource = _deserializer.Deserialize<T>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(T).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(T).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (resource == null) return BadRequest("No resource provided.");

            if (!await CanAccessResource(resource, "write"))
            {
                return Forbid();
            }

            var outcome = await _validator.ValidateAsync(resource);

            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            try
            {
                var id = await _repository.CreateAsync(resource, _searchIndexer);
                return CreatedAtAction(nameof(Read), new { id = id }, resource);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {ResourceType}", typeof(T).Name);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPut("{id}")]
        public virtual async Task<IActionResult> Update(string id, [FromBody] System.Text.Json.JsonElement body)
        {
            string jsonString = body.GetRawText();
            T resource;

            try
            {
                resource = _deserializer.Deserialize<T>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(T).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (resource == null) return BadRequest("No resource provided.");
            if (id != resource.Id) return BadRequest("ID mismatch");

            if (!await CanAccessResource(resource, "write"))
            {
                return Forbid();
            }

            var outcome = await _validator.ValidateAsync(resource);
            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            try
            {
                await _repository.UpdateAsync(id, resource, _searchIndexer);
                return Ok(resource);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating {ResourceType} {Id}", typeof(T).Name, id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPatch("{id}")]
        public virtual async Task<IActionResult> Patch(string id, [FromBody] System.Text.Json.JsonElement body)
        {
            var existingResource = await _repository.GetAsync<T>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            if (!await CanAccessResource(existingResource, "write"))
            {
                return Forbid();
            }

            var jsonString = body.GetRawText();
            T resource;

            try
            {
                resource = _deserializer.Deserialize<T>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(T).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (resource == null)
            {
                return BadRequest("No resource provided.");
            }

            if (string.IsNullOrWhiteSpace(resource.Id))
            {
                resource.Id = id;
            }
            else if (!string.Equals(id, resource.Id, StringComparison.Ordinal))
            {
                return BadRequest("ID mismatch");
            }

            var outcome = await _validator.ValidateAsync(resource);
            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            try
            {
                await _repository.UpdateAsync(id, resource, _searchIndexer);
                return Ok(resource);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error patching {ResourceType} {Id}", typeof(T).Name, id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpDelete("{id}")]
        public virtual async Task<IActionResult> Delete(string id)
        {
            var existingResource = await _repository.GetAsync<T>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            if (!await CanAccessResource(existingResource, "write"))
            {
                return Forbid();
            }

            try
            {
                await _repository.DeleteAsync(typeof(T).Name, id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting {ResourceType} {Id}", typeof(T).Name, id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("{id}/_history")]
        public virtual async Task<IActionResult> History(string id)
        {
            var resources = await _repository.GetHistoryAsync<T>(id);
            if (resources.Count == 0)
            {
                return NotFound();
            }

            var filteredResources = new List<T>();
            foreach (var resource in resources)
            {
                if (await CanAccessResource(resource, "read"))
                {
                    filteredResources.Add(resource);
                }
            }

            if (filteredResources.Count == 0)
            {
                return Forbid();
            }

            return Ok(filteredResources);
        }

        private async Task<bool> CanAccessResource(T resource, string action)
        {
            var accessLevel = _authorizationService.GetAccessLevel(User, typeof(T).Name, action);
            if (accessLevel == SmartAccessLevel.None)
            {
                return false;
            }

            if (accessLevel == SmartAccessLevel.System)
            {
                return true;
            }

            var indices = _searchIndexer.Extract(resource);

            if (accessLevel == SmartAccessLevel.User)
            {
                var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(User);
                return _authorizationService.IsResourceOwnedByOrganizations(resource, indices, organizationIds);
            }

            var patientId = _authorizationService.ResolvePatientId(User);
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return false;
            }

            return _authorizationService.IsResourceOwnedByPatient(resource, patientId, indices);
        }
    }
}
