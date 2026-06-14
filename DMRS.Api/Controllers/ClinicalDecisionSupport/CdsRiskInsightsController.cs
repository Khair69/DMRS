using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Infrastructure.Security;
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
        private readonly ISmartAuthorizationService _authorizationService;

        public CdsRiskInsightsController(
            IHighUtilizationRiskService riskService,
            IDiabetesRiskService diabetesRiskService,
            ICardiovascularRiskService cardiovascularRiskService,
            ISmartAuthorizationService authorizationService)
        {
            _riskService = riskService;
            _diabetesRiskService = diabetesRiskService;
            _cardiovascularRiskService = cardiovascularRiskService;
            _authorizationService = authorizationService;
        }

        // Scores every eligible patient in one request. The dashboard uses this instead of calling
        // the per-patient endpoint 100 times (the browser caps concurrent connections).
        // The cohort is scoped to the caller's accessible patients (null = all, for a system caller).
        [HttpGet("high-utilization/batch")]
        public async Task<IActionResult> GetHighUtilizationRiskBatch([FromQuery] bool mine, CancellationToken cancellationToken)
        {
            var patientIdFilter = await _authorizationService.ResolveViewPatientIdsAsync(User, mine);
            var results = await _riskService.AssessAllAsync(patientIdFilter, cancellationToken);
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

        // Scores every eligible patient in one request (see high-utilization/batch). Used by the
        // AI Insights page to show a population view per model without 100 per-patient calls.
        [HttpGet("diabetes/batch")]
        public async Task<IActionResult> GetDiabetesRiskBatch([FromQuery] bool mine, CancellationToken cancellationToken)
        {
            var patientIdFilter = await _authorizationService.ResolveViewPatientIdsAsync(User, mine);
            var results = await _diabetesRiskService.AssessAllAsync(patientIdFilter, cancellationToken);
            return Ok(results);
        }

        [HttpGet("cardiovascular/batch")]
        public async Task<IActionResult> GetCardiovascularRiskBatch([FromQuery] bool mine, CancellationToken cancellationToken)
        {
            var patientIdFilter = await _authorizationService.ResolveViewPatientIdsAsync(User, mine);
            var results = await _cardiovascularRiskService.AssessAllAsync(patientIdFilter, cancellationToken);
            return Ok(results);
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
