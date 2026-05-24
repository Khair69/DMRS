using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.ClinicalDecisionSupport
{
    [ApiController]
    [Route("cds/risk")]
    [Authorize(Policy = "FhirScope")]
    public sealed class CdsRiskInsightsController : ControllerBase
    {
        private readonly IHighUtilizationRiskService _riskService;

        public CdsRiskInsightsController(IHighUtilizationRiskService riskService)
        {
            _riskService = riskService;
        }

        [HttpGet("high-utilization/{patientId}")]
        public async Task<IActionResult> GetHighUtilizationRisk(string patientId, CancellationToken cancellationToken)
        {
            var result = await _riskService.AssessPatientAsync(patientId, cancellationToken);
            return result == null ? NotFound() : Ok(result);
        }
    }
}
