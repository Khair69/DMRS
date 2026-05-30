using DMRS.Api.Domain.Interfaces;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers
{
    [ApiController]
    [Route("analytics")]
    [Authorize(Policy = "FhirScope")]
    public sealed class AnalyticsController : ControllerBase
    {
        private readonly IFhirRepository _fhirRepository;

        public AnalyticsController(IFhirRepository fhirRepository)
        {
            _fhirRepository = fhirRepository;
        }

        /// <summary>
        /// Returns the top 10 most common active conditions across all patients.
        /// </summary>
        [HttpGet("condition-prevalence")]
        public async Task<IActionResult> GetConditionPrevalence(CancellationToken cancellationToken)
        {
            var allConditions = await _fhirRepository.SearchAsync<Condition>(
                new Dictionary<string, string>());

            var prevalence = allConditions
                .Select(c =>
                    c.Code?.Text
                    ?? c.Code?.Coding?.FirstOrDefault()?.Display
                    ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { name = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToList();

            return Ok(prevalence);
        }
    }
}
