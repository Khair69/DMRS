using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Search.Medication;
using DMRS.Api.Infrastructure.Security;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Medication
{
    public class MedicationRequestController : FhirBaseController<MedicationRequest>
    {
        private readonly IClinicalDecisionSupportService _cds;

        public MedicationRequestController(
            IFhirRepository repository,
            ILogger<MedicationRequestController> logger,
            FhirJsonDeserializer deserializer,
            FhirJsonSerializer serializer,
            IFhirValidatorService validator,
            MedicationRequestIndexer searchIndexer,
            ISmartAuthorizationService authorizationService,
            IClinicalDecisionSupportService cds)
            : base(repository, logger, deserializer, serializer, validator, searchIndexer, authorizationService)
        {
            _cds = cds;
        }

        [HttpPost]
        public override async Task<IActionResult> Create([FromBody] System.Text.Json.JsonElement body)
        {
            string jsonString = body.GetRawText();
            MedicationRequest resource;

            try
            {
                resource = _deserializer.Deserialize<MedicationRequest>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(MedicationRequest).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(MedicationRequest).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (resource == null) return BadRequest("No resource provided.");

            if (!await CanCreateResource(resource))
            {
                return Forbid();
            }

            var outcome = await _validator.ValidateAsync(resource);

            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            var cdsResult = await _cds.EvaluateMedicationRequestAsync(resource);
            if (cdsResult?.HasErrors == true)
            {
                return UnprocessableEntity(cdsResult.Outcome);
            }
            AddCdsWarnings(cdsResult);

            try
            {
                var id = await _repository.CreateAsync(resource, _searchIndexer);
                Response.Headers.ETag = $"W/\"{resource.Meta?.VersionId}\"";
                Response.Headers.Location = $"/fhir/{typeof(MedicationRequest).Name}/{id}";

                return StatusCode(201, _serializer.SerializeToString(resource));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {ResourceType}", typeof(MedicationRequest).Name);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPut("{id}")]
        public override async Task<IActionResult> Update(string id, [FromBody] System.Text.Json.JsonElement body)
        {
            var existingResource = await _repository.GetAsync<MedicationRequest>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            string jsonString = body.GetRawText();
            MedicationRequest resource;

            try
            {
                resource = _deserializer.Deserialize<MedicationRequest>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(MedicationRequest).Name);
                return BadRequest("Invalid FHIR content: " + ex.Message);
            }

            if (resource == null) return BadRequest("No resource provided.");
            if (id != resource.Id) return BadRequest("ID mismatch");

            if (!await CanUpdateResource(existingResource, resource))
            {
                return Forbid();
            }

            var outcome = await _validator.ValidateAsync(resource);
            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            var cdsResult = await _cds.EvaluateMedicationRequestAsync(resource);
            if (cdsResult?.HasErrors == true)
            {
                return UnprocessableEntity(cdsResult.Outcome);
            }
            AddCdsWarnings(cdsResult);

            try
            {
                await _repository.UpdateAsync(id, resource, _searchIndexer);
                return Ok(_serializer.SerializeToString(resource));
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
                _logger.LogError(ex, "Error updating {ResourceType} {Id}", typeof(MedicationRequest).Name, id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPatch("{id}")]
        public override async Task<IActionResult> Patch(string id, [FromBody] System.Text.Json.JsonElement body)
        {
            var existingResource = await _repository.GetAsync<MedicationRequest>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            var jsonString = body.GetRawText();
            MedicationRequest resource;

            try
            {
                resource = _deserializer.Deserialize<MedicationRequest>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(MedicationRequest).Name);
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

            if (!await CanUpdateResource(existingResource, resource))
            {
                return Forbid();
            }

            var outcome = await _validator.ValidateAsync(resource);
            if (!outcome.Success)
            {
                return BadRequest(outcome);
            }

            var cdsResult = await _cds.EvaluateMedicationRequestAsync(resource);
            if (cdsResult?.HasErrors == true)
            {
                return UnprocessableEntity(cdsResult.Outcome);
            }
            AddCdsWarnings(cdsResult);

            try
            {
                await _repository.UpdateAsync(id, resource, _searchIndexer);
                return Ok(_serializer.SerializeToString(resource));
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
                _logger.LogError(ex, "Error patching {ResourceType} {Id}", typeof(MedicationRequest).Name, id);
                return StatusCode(500, "Internal Server Error");
            }
        }

        private void AddCdsWarnings(CdsEvaluationResult? result)
        {
            if (result?.HasWarnings != true)
            {
                return;
            }

            var warnings = result.Alerts
                .Where(a => a.Severity == OperationOutcome.IssueSeverity.Warning)
                .Select(a => a.Message)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .ToList();

            if (warnings.Count == 0)
            {
                return;
            }

            Response.Headers.Append("X-CDS-Warnings", string.Join(" | ", warnings));
        }
    }
}

