using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
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

        public CdsRulesController(IRuleManagementService ruleManagement)
        {
            _ruleManagement = ruleManagement;
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
            var rule = await _ruleManagement.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] CdsRuleDefinition request, CancellationToken cancellationToken)
        {
            var updated = await _ruleManagement.UpdateAsync(id, request, cancellationToken);
            return updated == null ? NotFound() : Ok(updated);
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
    }
}
