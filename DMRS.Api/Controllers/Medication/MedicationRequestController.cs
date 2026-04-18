using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
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
        private readonly IMedicationRequestKnowledgeWarmup _knowledgeWarmup;

        public MedicationRequestController(
            IFhirRepository repository,
            ILogger<MedicationRequestController> logger,
            FhirJsonDeserializer deserializer,
            FhirJsonSerializer serializer,
            IFhirValidatorService validator,
            MedicationRequestIndexer searchIndexer,
            ISmartAuthorizationService authorizationService,
            IMedicationRequestKnowledgeWarmup knowledgeWarmup)
            : base(repository, logger, deserializer, serializer, validator, searchIndexer, authorizationService)
        {
            _knowledgeWarmup = knowledgeWarmup;
        }

        [HttpPost]
        public override async Task<IActionResult> Create([FromBody] JsonElement body)
        {
            var resource = Deserialize(body, out var errorResult);
            if (errorResult != null)
            {
                return errorResult;
            }

            if (resource == null)
            {
                return BadRequest("No resource provided.");
            }

            if (!await CanCreateResource(resource))
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
                Response.Headers.ETag = $"W/\"{resource.Meta?.VersionId}\"";
                Response.Headers.Location = $"/fhir/{typeof(MedicationRequest).Name}/{id}";

                await _knowledgeWarmup.WarmAsync(resource, HttpContext.RequestAborted);
                return StatusCode(201, _serializer.SerializeToString(resource));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {ResourceType}", typeof(MedicationRequest).Name);
                return StatusCode(500, "Internal Server Error");
            }
        }

        [HttpPut("{id}")]
        public override async Task<IActionResult> Update(string id, [FromBody] JsonElement body)
        {
            var existingResource = await _repository.GetAsync<MedicationRequest>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            var resource = Deserialize(body, out var errorResult);
            if (errorResult != null)
            {
                return errorResult;
            }

            if (resource == null)
            {
                return BadRequest("No resource provided.");
            }

            if (id != resource.Id)
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

            try
            {
                await _repository.UpdateAsync(id, resource, _searchIndexer);
                await _knowledgeWarmup.WarmAsync(resource, HttpContext.RequestAborted);
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
        public override async Task<IActionResult> Patch(string id, [FromBody] JsonElement body)
        {
            var existingResource = await _repository.GetAsync<MedicationRequest>(id);
            if (existingResource == null)
            {
                return NotFound();
            }

            var resource = Deserialize(body, out var errorResult);
            if (errorResult != null)
            {
                return errorResult;
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

            try
            {
                await _repository.UpdateAsync(id, resource, _searchIndexer);
                await _knowledgeWarmup.WarmAsync(resource, HttpContext.RequestAborted);
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

        private MedicationRequest? Deserialize(JsonElement body, out IActionResult? errorResult)
        {
            var jsonString = body.GetRawText();

            try
            {
                errorResult = null;
                return _deserializer.Deserialize<MedicationRequest>(jsonString);
            }
            catch (DeserializationFailedException ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(MedicationRequest).Name);
                errorResult = BadRequest("Invalid FHIR content: " + ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize FHIR content for {ResourceType}", typeof(MedicationRequest).Name);
                errorResult = BadRequest("Invalid FHIR content: " + ex.Message);
                return null;
            }
        }
    }
}
