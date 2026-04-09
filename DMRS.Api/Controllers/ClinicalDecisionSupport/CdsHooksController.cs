using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
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

        public CdsHooksController(ICdsHookService hookService, ICdsServiceRegistry serviceRegistry)
        {
            _hookService = hookService;
            _serviceRegistry = serviceRegistry;
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
            return Ok(response);
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
