using DMRS.Api.Application.Documents;
using DMRS.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DMRS.Api.Controllers.Clinical
{
    [ApiController]
    [Route("api/patients/{patientId}/documents")]
    [Authorize(Policy = "FhirScope")]
    public sealed class PatientDocumentsController : ControllerBase
    {
        private readonly IPatientDocumentService _documentService;
        private readonly ISmartAuthorizationService _authorizationService;
        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

        public PatientDocumentsController(
            IPatientDocumentService documentService,
            ISmartAuthorizationService authorizationService)
        {
            _documentService = documentService;
            _authorizationService = authorizationService;
        }

        [HttpGet]
        public async Task<IActionResult> List(string patientId, CancellationToken cancellationToken)
        {
            if (!await CanAccessPatientAsync(patientId, "read"))
            {
                return Forbid();
            }

            var records = await _documentService.ListAsync(patientId);
            return Ok(records);
        }

        [HttpPost]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Upload(string patientId, IFormFile file, CancellationToken cancellationToken)
        {
            if (!await CanAccessPatientAsync(patientId, "write"))
            {
                return Forbid();
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest("No file provided.");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest("File exceeds the 20 MB limit.");
            }

            var uploadedBy = User.Identity?.Name ?? User.FindFirst("preferred_username")?.Value ?? "unknown";

            await using var stream = file.OpenReadStream();
            var record = await _documentService.SaveAsync(
                patientId,
                file.FileName,
                file.ContentType,
                stream,
                uploadedBy);

            return Created($"api/patients/{patientId}/documents/{record.Id}/content", record);
        }

        [HttpGet("{documentId}/content")]
        public async Task<IActionResult> Download(string patientId, string documentId, CancellationToken cancellationToken)
        {
            if (!await CanAccessPatientAsync(patientId, "read"))
            {
                return Forbid();
            }

            var result = await _documentService.GetContentAsync(patientId, documentId);
            if (result is null)
            {
                return NotFound();
            }

            var (record, content) = result.Value;
            return File(content, record.ContentType, record.FileName);
        }

        [HttpDelete("{documentId}")]
        public async Task<IActionResult> Delete(string patientId, string documentId, CancellationToken cancellationToken)
        {
            if (!await CanAccessPatientAsync(patientId, "delete"))
            {
                return Forbid();
            }

            var deleted = await _documentService.DeleteAsync(patientId, documentId);
            return deleted ? NoContent() : NotFound();
        }

        /// <summary>
        /// Patient documents are scoped to their owning patient, but the FhirScope policy only checks
        /// resource ownership when the route exposes an "id" parameter — this controller's parameter is
        /// "patientId", so that check is skipped. Enforce ownership here against the resolved patient:
        /// a patient caller may only touch their own record; an org (User) caller only patients in their
        /// organization; a system caller anyone.
        /// </summary>
        private async Task<bool> CanAccessPatientAsync(string patientId, string action)
        {
            if (string.IsNullOrWhiteSpace(patientId))
            {
                return false;
            }

            var accessLevel = _authorizationService.GetAccessLevel(User, "Patient", action);

            switch (accessLevel)
            {
                case SmartAccessLevel.System:
                    return true;

                case SmartAccessLevel.User:
                    // Staff may read any patient's documents across orgs; upload/delete stay org-scoped.
                    if (action == "read")
                    {
                        return true;
                    }

                    var organizationIds = await _authorizationService.ResolveOrganizationIdsAsync(User);
                    return await _authorizationService.IsResourceOwnedByOrganizationsAsync("Patient", patientId, organizationIds);

                case SmartAccessLevel.Patient:
                    var callerPatientId = _authorizationService.ResolvePatientId(User);
                    return !string.IsNullOrWhiteSpace(callerPatientId)
                        && string.Equals(callerPatientId, patientId, StringComparison.OrdinalIgnoreCase);

                default:
                    return false;
            }
        }
    }
}
