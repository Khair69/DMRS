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
        private readonly IDiabetesRiskService _diabetesRiskService;
        private readonly ICardiovascularRiskService _cardiovascularRiskService;

        public CdsRiskInsightsController(
            IHighUtilizationRiskService riskService,
            IDiabetesRiskService diabetesRiskService,
            ICardiovascularRiskService cardiovascularRiskService)
        {
            _riskService = riskService;
            _diabetesRiskService = diabetesRiskService;
            _cardiovascularRiskService = cardiovascularRiskService;
        }

        // Scores every eligible patient in one request. The dashboard uses this instead of calling
        // the per-patient endpoint 100 times (the browser caps concurrent connections).
        [HttpGet("high-utilization/batch")]
        public async Task<IActionResult> GetHighUtilizationRiskBatch(CancellationToken cancellationToken)
        {
            var results = await _riskService.AssessAllAsync(cancellationToken);
            return Ok(results);
        }

        // Returns 200 with a null body when the model is unavailable so the patient chart, which
        // loads all risk scores together, never fails just because the model has not been trained yet.
        [HttpGet("high-utilization/{patientId}")]
        public async Task<IActionResult> GetHighUtilizationRisk(string patientId, CancellationToken cancellationToken)
        {
            var result = await _riskService.AssessPatientAsync(patientId, cancellationToken);
            return Ok(result);
        }

        // Returns 200 with a null body when the model is unavailable so the patient chart, which
        // loads all risk scores together, never fails just because a model has not been trained yet.
        [HttpGet("diabetes/{patientId}")]
        public async Task<IActionResult> GetDiabetesRisk(string patientId, CancellationToken cancellationToken)
        {
            var result = await _diabetesRiskService.AssessPatientAsync(patientId, cancellationToken);
            return Ok(result);
        }

        [HttpGet("cardiovascular/{patientId}")]
        public async Task<IActionResult> GetCardiovascularRisk(string patientId, CancellationToken cancellationToken)
        {
            var result = await _cardiovascularRiskService.AssessPatientAsync(patientId, cancellationToken);
            return Ok(result);
        }
    }
}
