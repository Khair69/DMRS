using DMRS.Api.Application.ExternalAi.Interfaces;
using DMRS.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.ExternalAi
{
    /// <summary>
    /// Runs a registered external AI model against a single patient. Open to any clinical caller, but
    /// the caller must be authorized to view the patient before any data leaves the system, and every
    /// send is audit-logged (who / which model / which patient).
    /// </summary>
    [ApiController]
    [Route("external-ai/infer")]
    [Authorize(Policy = "FhirScope")]
    public sealed class ExternalAiInferenceController : ControllerBase
    {
        private readonly IExternalAiInferenceService _inferenceService;
        private readonly ISmartAuthorizationService _authorizationService;
        private readonly ILogger<ExternalAiInferenceController> _logger;

        public ExternalAiInferenceController(
            IExternalAiInferenceService inferenceService,
            ISmartAuthorizationService authorizationService,
            ILogger<ExternalAiInferenceController> logger)
        {
            _inferenceService = inferenceService;
            _authorizationService = authorizationService;
            _logger = logger;
        }

        [HttpPost("{modelId:guid}/{patientId}")]
        public async Task<IActionResult> Run(Guid modelId, string patientId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return BadRequest(new { error = "A patient id is required." });
            }

            var normalizedPatientId = patientId.StartsWith("Patient/", StringComparison.OrdinalIgnoreCase)
                ? patientId["Patient/".Length..]
                : patientId;

            // Fail closed: the caller must be able to view this patient. A null set = unrestricted
            // (system) caller; otherwise the patient must be in the accessible set.
            var accessiblePatientIds = await _authorizationService.ResolveViewPatientIdsAsync(User, panelOnly: false);
            if (accessiblePatientIds is not null
                && !accessiblePatientIds.Contains(normalizedPatientId, StringComparer.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            // Audit the send BEFORE the data leaves the system, so an attempt is recorded even if the
            // remote call later fails or hangs.
            _logger.LogInformation(
                "External AI send: user {User} → model {ModelId} for patient {PatientId}.",
                User.Identity?.Name ?? "(unknown)", modelId, normalizedPatientId);

            var result = await _inferenceService.RunAsync(modelId, normalizedPatientId, cancellationToken);
            if (result is null)
            {
                return NotFound(new { error = "No active external AI model with that id." });
            }

            return Ok(result);
        }
    }
}
