using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.ClinicalDecisionSupport
{
    [ApiController]
    [Route("cds/rules")]
    [Authorize(Policy = "FhirScope")]
    public sealed class CdsRulesController : ControllerBase
    {
        private readonly IRuleManagementService _ruleManagement;
        private readonly ICdsVariableCatalog _variableCatalog;

        public CdsRulesController(IRuleManagementService ruleManagement, ICdsVariableCatalog variableCatalog)
        {
            _ruleManagement = ruleManagement;
            _variableCatalog = variableCatalog;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var rules = await _ruleManagement.ListAsync(cancellationToken);
            return Ok(rules);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        {
            var rule = await _ruleManagement.GetAsync(id, cancellationToken);
            return rule == null ? NotFound() : Ok(rule);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CdsRuleDefinition request, CancellationToken cancellationToken)
        {
            try
            {
                var rule = await _ruleManagement.CreateAsync(request, cancellationToken);
                return CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CdsRuleDefinition request, CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _ruleManagement.UpdateAsync(id, request, cancellationToken);
                return updated == null ? NotFound() : Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id:guid}/activate")]
        public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
        {
            var updated = await _ruleManagement.ActivateAsync(id, true, cancellationToken);
            return updated ? NoContent() : NotFound();
        }

        [HttpPatch("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
        {
            var updated = await _ruleManagement.ActivateAsync(id, false, cancellationToken);
            return updated ? NoContent() : NotFound();
        }

        [HttpPost("validate")]
        public ActionResult<RuleValidationResult> Validate([FromBody] CdsRuleDefinition request)
        {
            return Ok(_ruleManagement.Validate(request));
        }

        [HttpPost("preview")]
        public async Task<ActionResult<RulePreviewResponse>> Preview(
            [FromBody] RulePreviewRequest request,
            CancellationToken cancellationToken)
        {
            var response = await _ruleManagement.PreviewAsync(request, cancellationToken);
            return Ok(response);
        }

        [HttpGet("variables")]
        public IActionResult Variables()
        {
            return Ok(_variableCatalog.ListVariables());
        }
    }
}
