using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Application.ClinicalDecisionSupport.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.ClinicalDecisionSupport
{
    [ApiController]
    [Route("cds-services")]
    [Authorize(Policy = "FhirScope")]
    public sealed class CdsHooksController : ControllerBase
    {
        private readonly ICdsHookService _hookService;
        private readonly ICdsServiceRegistry _serviceRegistry;
        private readonly CdsAlertFeed _alertFeed;

        public CdsHooksController(
            ICdsHookService hookService,
            ICdsServiceRegistry serviceRegistry,
            CdsAlertFeed alertFeed)
        {
            _hookService = hookService;
            _serviceRegistry = serviceRegistry;
            _alertFeed = alertFeed;
        }

        [HttpGet]
        public IActionResult GetServices()
        {
            var services = _serviceRegistry.ListServices();
            return Ok(new { services });
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Execute(string id, [FromBody] JsonElement body, CancellationToken cancellationToken)
        {
            var service = _serviceRegistry.GetService(id);
            if (service == null)
            {
                return NotFound();
            }

            var request = ParseRequest(body, service);
            if (request == null)
            {
                return BadRequest("Invalid CDS Hooks request payload.");
            }

            var response = await _hookService.EvaluateAsync(service.Hook, request, cancellationToken);

            if (response.Cards.Count > 0)
            {
                var patientId = ExtractPatientId(request.Context);
                _alertFeed.Enqueue(patientId, service.Hook, response.Cards);
            }

            return Ok(response);
        }

        private static string ExtractPatientId(JsonElement context)
        {
            // CDS Hooks context typically carries patientId at the top level
            if (context.TryGetProperty("patientId", out var pid) && pid.ValueKind == JsonValueKind.String)
                return pid.GetString() ?? "unknown";

            // Fallback: some hooks nest it under patient.id
            if (context.TryGetProperty("patient", out var pt))
            {
                if (pt.ValueKind == JsonValueKind.String)
                    return pt.GetString()?.Replace("Patient/", "") ?? "unknown";

                if (pt.ValueKind == JsonValueKind.Object
                    && pt.TryGetProperty("id", out var id)
                    && id.ValueKind == JsonValueKind.String)
                    return id.GetString() ?? "unknown";
            }

            return "unknown";
        }

        private static CdsHookRequest? ParseRequest(JsonElement body, CdsServiceDefinition service)
        {
            if (body.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var hookInstance = body.TryGetProperty("hookInstance", out var hookInstanceElement)
                && hookInstanceElement.ValueKind == JsonValueKind.String
                ? hookInstanceElement.GetString()
                : Guid.NewGuid().ToString("N");

            if (!body.TryGetProperty("context", out var contextElement))
            {
                return null;
            }

            var prefetch = body.TryGetProperty("prefetch", out var prefetchElement)
                ? prefetchElement
                : (JsonElement?)null;

            return new CdsHookRequest(
                service.Hook,
                hookInstance ?? Guid.NewGuid().ToString("N"),
                contextElement,
                prefetch);
        }
    }
}
