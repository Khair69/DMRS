using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PatientController : ControllerBase
    {
        private readonly IFhirRepository _repository;
        private readonly ILogger<PatientController> _logger;
        private readonly FhirJsonDeserializer _deserializer;

        public PatientController(IFhirRepository repository, ILogger<PatientController> logger, FhirJsonDeserializer deserializer)
        {
            _repository = repository;
            _logger = logger;
            _deserializer = deserializer;
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

            // 1. Basic Validation
            if (patient == null) return BadRequest("Invalid FHIR content");

            // 2. Structural Validation (using Firely Validator)
            // (Add logic here to check if required fields like Name/Identifier exist)

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
    }
}
