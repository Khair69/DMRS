using DMRS.Api.Application.ExternalAi.Interfaces;
using DMRS.Api.Application.ExternalAi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.ExternalAi
{
    /// <summary>
    /// Admin CRUD for the external ("away") AI model registry. Restricted to admins because a
    /// registered endpoint determines where patient data is sent.
    /// </summary>
    [ApiController]
    [Route("external-ai/models")]
    [Authorize(Policy = "FhirScope", Roles = "ROLE_SYSTEM_ADMIN,ROLE_ORG_ADMIN")]
    public sealed class ExternalAiModelsController : ControllerBase
    {
        private readonly IExternalAiModelManagementService _management;

        public ExternalAiModelsController(IExternalAiModelManagementService management)
        {
            _management = management;
        }

        [HttpGet]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var models = await _management.ListAsync(cancellationToken);
            return Ok(models);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        {
            var model = await _management.GetAsync(id, cancellationToken);
            return model is null ? NotFound() : Ok(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ExternalAiModelInput input, CancellationToken cancellationToken)
        {
            try
            {
                var created = await _management.CreateAsync(input, User.Identity?.Name, cancellationToken);
                return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
            }
            catch (ExternalAiModelValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ExternalAiModelInput input, CancellationToken cancellationToken)
        {
            try
            {
                var updated = await _management.UpdateAsync(id, input, User.Identity?.Name, cancellationToken);
                return updated is null ? NotFound() : Ok(updated);
            }
            catch (ExternalAiModelValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            var deleted = await _management.DeleteAsync(id, cancellationToken);
            return deleted ? NoContent() : NotFound();
        }
    }
}
