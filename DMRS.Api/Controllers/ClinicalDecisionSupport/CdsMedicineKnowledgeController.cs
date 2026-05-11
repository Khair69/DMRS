using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.ClinicalDecisionSupport
{
    [ApiController]
    [Route("cds/medications")]
    [Authorize(Policy = "FhirScope")]
    public sealed class CdsMedicineKnowledgeController : ControllerBase
    {
        private readonly IMedicineKnowledgeService _medicineKnowledgeService;

        public CdsMedicineKnowledgeController(IMedicineKnowledgeService medicineKnowledgeService)
        {
            _medicineKnowledgeService = medicineKnowledgeService;
        }

        [HttpGet]
        public async Task<IActionResult> Search(
            [FromQuery] string? q,
            [FromQuery] string? ingredient,
            [FromQuery] string? indication,
            [FromQuery] int limit = 25,
            CancellationToken cancellationToken = default)
        {
            var results = await _medicineKnowledgeService.SearchAsync(q, ingredient, indication, limit, cancellationToken);
            return Ok(results);
        }

        [HttpGet("{value}")]
        public async Task<IActionResult> Get(string value, CancellationToken cancellationToken)
        {
            var result = await _medicineKnowledgeService.GetAsync(value, cancellationToken);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpPost("{value}/refresh")]
        public async Task<IActionResult> Refresh(string value, CancellationToken cancellationToken)
        {
            var result = await _medicineKnowledgeService.RefreshAsync(value, cancellationToken);
            return result == null ? NotFound() : Ok(result);
        }
    }
}
