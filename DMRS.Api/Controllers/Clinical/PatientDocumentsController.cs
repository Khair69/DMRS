using DMRS.Api.Application.Documents;
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
        private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

        public PatientDocumentsController(IPatientDocumentService documentService)
        {
            _documentService = documentService;
        }

        [HttpGet]
        public async Task<IActionResult> List(string patientId, CancellationToken cancellationToken)
        {
            var records = await _documentService.ListAsync(patientId);
            return Ok(records);
        }

        [HttpPost]
        [RequestSizeLimit(20 * 1024 * 1024)]
        public async Task<IActionResult> Upload(string patientId, IFormFile file, CancellationToken cancellationToken)
        {
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
            var deleted = await _documentService.DeleteAsync(patientId, documentId);
            return deleted ? NoContent() : NotFound();
        }
    }
}
