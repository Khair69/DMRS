using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.ClinicalDecisionSupport
{
    [ApiController]
    [Route("cds/rules")]
    [Authorize(Policy = "CdsAdmin")]
    public sealed class CdsRulesController : ControllerBase
    {
        private readonly IRuleManagementService _ruleManagement;
        private readonly ICdsVariableCatalog _variableCatalog;
        private readonly IRuleTemplateService _ruleTemplateService;

        public CdsRulesController(
            IRuleManagementService ruleManagement,
            ICdsVariableCatalog variableCatalog,
            IRuleTemplateService ruleTemplateService)
        {
            _ruleManagement = ruleManagement;
            _variableCatalog = variableCatalog;
            _ruleTemplateService = ruleTemplateService;
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

        [HttpGet("{id:guid}/versions")]
        public async Task<IActionResult> Versions(Guid id, CancellationToken cancellationToken)
        {
            var rule = await _ruleManagement.GetAsync(id, cancellationToken);
            if (rule == null)
            {
                return NotFound();
            }

            var versions = await _ruleManagement.ListVersionsAsync(id, cancellationToken);
            return Ok(versions);
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

        [HttpPost("{id:guid}/publish")]
        public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _ruleManagement.PublishAsync(id, cancellationToken);
                return updated == null ? NotFound() : Ok(updated);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPatch("{id:guid}/archive")]
        public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
        {
            var updated = await _ruleManagement.ArchiveAsync(id, cancellationToken);
            return updated ? NoContent() : NotFound();
        }

        [HttpPost("{id:guid}/clone")]
        public async Task<IActionResult> Clone(Guid id, CancellationToken cancellationToken)
        {
            var rule = await _ruleManagement.CloneAsync(id, cancellationToken);
            return rule == null ? NotFound() : CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
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

        [HttpGet("templates")]
        public IActionResult Templates()
        {
            return Ok(_ruleTemplateService.ListTemplates());
        }

        [HttpPost("templates/compile")]
        public IActionResult CompileTemplate([FromBody] RuleTemplateRequest request)
        {
            try
            {
                var rule = _ruleTemplateService.Compile(request);
                return Ok(rule);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("templates")]
        public async Task<IActionResult> CreateFromTemplate([FromBody] RuleTemplateRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var compiled = _ruleTemplateService.Compile(request);
                var rule = await _ruleManagement.CreateAsync(compiled, cancellationToken);
                return CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
