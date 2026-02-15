using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
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
        protected readonly IFhirValidatorService _validator;
        protected readonly ISearchIndexer _searchIndexer;

        public FhirBaseController(IFhirRepository repository, ILogger logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator, ISearchIndexer searchIndexer)
        {
            _repository = repository;
            _logger = logger;
            _deserializer = deserializer;
            _validator = validator;
            _searchIndexer = searchIndexer;
        }

        [HttpGet("{id}")]
        public virtual async Task<IActionResult> Read(string id)
        {
            var resource = await _repository.GetAsync<T>(id);
            if (resource == null) return NotFound();
            return Ok(resource);
        }

        [HttpGet("{id}/_history/{vid}")]
        public virtual async Task<IActionResult> VRead(string id, string vid)
        {
            return Ok();
        }

        [HttpGet("search/{searchParam}/{value}")]
        public virtual async Task<IActionResult> Search(string searchParam, string value)
        {
            var resources = await _repository.SearchAsync<T>(searchParam, value);
            if (resources.Count == 0) return NotFound();
            return Ok(resources);
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

            if (resource == null) return BadRequest("No resource provided.");

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

        //[HttpPut("{id}")]
        //public virtual async Task<IActionResult> Patch(string id, [FromBody] T resource)
        //{
        //    if (id != resource.Id) return BadRequest("ID mismatch");
        //    return Ok(resource);
        //}

        [HttpDelete("{id}")]
        public virtual async Task<IActionResult> Delete(string id)
        {
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
            return Ok();
        }
    }
}
