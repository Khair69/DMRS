using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Migrations;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    public class PatientController : FhirBaseController
    {
        private readonly IFhirRepository _repository;
        private readonly ILogger<PatientController> _logger;
        private readonly FhirJsonDeserializer _deserializer;
        private readonly IFhirValidatorService _validator;

        public PatientController(IFhirRepository repository, ILogger<PatientController> logger, FhirJsonDeserializer deserializer, IFhirValidatorService validator)
        {
            _repository = repository;
            _logger = logger;
            _deserializer = deserializer;
            _validator = validator;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] System.Text.Json.JsonElement body)
        {
            string jsonString = body.GetRawText();

            Patient patient;
            try
            {
                patient = _deserializer.Deserialize<Patient>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content");
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            //Validation
            if (patient == null) return BadRequest("No resource provided.");

            var outcome = await _validator.ValidateAsync(patient);

            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            try
            {
                var id = await _repository.CreateAsync(patient);

                // FHIR requires Location header on 201 Created
                return CreatedAtAction(nameof(Get), new { id = id }, patient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient");
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            var patient = await _repository.GetAsync<Patient>(id);
            if (patient == null) return NotFound();
            return Ok(patient);
        }

        [HttpGet("search/{searchParam}/{value}")]
        public async Task<IActionResult> Search(string searchParam, string value)
        {
            var patients = await _repository.SearchAsync<Patient>(searchParam, value);
            if (patients.Count == 0) return NotFound();
            return Ok(patients);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] System.Text.Json.JsonElement body)
        {
            string jsonString = body.GetRawText();

            Patient patient;
            try
            {
                patient = _deserializer.Deserialize<Patient>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content");
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (patient == null) return BadRequest("No resource provided.");

            var outcome = await _validator.ValidateAsync(patient);
            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            try
            {
                await _repository.UpdateAsync(id, patient);
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
                _logger.LogError(ex, "Error updating patient {Id}", id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _repository.DeleteAsync(nameof(Patient), id);
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
                _logger.LogError(ex, "Error deleting patient {Id}", id);
                return StatusCode(500, "Internal Server Error");
            }
        }

    }
}
