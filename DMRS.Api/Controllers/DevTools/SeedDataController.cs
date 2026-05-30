using DMRS.Api.Application.DevTools;
using DMRS.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace DMRS.Api.Controllers.DevTools;

/// <summary>
/// Development-only endpoints for loading sample data into the database.
/// Every action returns 404 when the host environment is not Development,
/// so this surface is invisible in staging or production.
/// </summary>
[ApiController]
[Route("dev")]
[Authorize(Policy = "FhirScope")]
public sealed class SeedDataController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly SeedDataService _seedService;
    private readonly AppDbContext _db;

    public SeedDataController(
        IWebHostEnvironment env,
        SeedDataService seedService,
        AppDbContext db)
    {
        _env = env;
        _seedService = seedService;
        _db = db;
    }

    /// <summary>
    /// Returns the current patient count so the client can show whether the
    /// database already has sample data.
    /// </summary>
    [HttpGet("seed/status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var count = await _db.FhirResources
            .CountAsync(r => r.ResourceType == "Patient" && !r.IsDeleted, ct);

        return Ok(new SeedStatusResult { PatientCount = count });
    }

    /// <summary>
    /// Returns the list of available sample-data file names so the client can
    /// iterate through them one at a time.
    /// </summary>
    [HttpGet("seed/files")]
    public IActionResult GetFiles()
    {
        if (!_env.IsDevelopment()) return NotFound();

        var dataDir = Path.Combine(_env.ContentRootPath, "sampledata", "fhir");
        if (!Directory.Exists(dataDir))
            return NotFound(new { error = "Sample data directory not found.", path = dataDir });

        var files = Directory.GetFiles(dataDir, "*.json")
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToList();

        return Ok(new { files, count = files.Count });
    }

    /// <summary>
    /// Seeds a single named file. The client calls this in a loop, one file
    /// per HTTP request, so no single request can time out on a large dataset.
    /// </summary>
    [HttpPost("seed/file")]
    public async Task<IActionResult> SeedFile(
        [FromBody] SeedFileRequest request,
        CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        if (string.IsNullOrWhiteSpace(request.FileName))
            return BadRequest("fileName is required.");

        // Strip any path components — only the bare file name is accepted.
        var safeFileName = Path.GetFileName(request.FileName);
        var dataDir      = Path.Combine(_env.ContentRootPath, "sampledata", "fhir");
        var filePath     = Path.Combine(dataDir, safeFileName);

        if (!System.IO.File.Exists(filePath))
            return NotFound(new { error = $"File '{safeFileName}' not found." });

        var result = await _seedService.SeedSingleFileAsync(filePath, ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns counts of un-indexed resources per type so the client knows
    /// which types need repair before starting the per-type loop.
    /// </summary>
    [HttpGet("seed/reindex/status")]
    public async Task<IActionResult> GetReindexStatus(CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        var counts = await _seedService.GetReindexStatusAsync(ct);
        return Ok(counts);
    }

    /// <summary>
    /// Re-indexes all un-indexed resources of a single named type.
    /// The client calls this in a loop — one request per type — so no single
    /// request has to load thousands of JSON documents at once.
    /// </summary>
    [HttpPost("seed/reindex/{resourceType}")]
    public async Task<IActionResult> ReindexType(string resourceType, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        var repaired = await _seedService.ReindexResourceTypeAsync(resourceType, ct);
        return Ok(new { resourceType, reindexed = repaired });
    }

    /// <summary>
    /// Convenience endpoint that seeds every file in one request.
    /// Use only when a long-running request is acceptable; prefer the
    /// file-by-file approach (/dev/seed/file) from browser clients.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> Seed(CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var dataDir = Path.Combine(_env.ContentRootPath, "sampledata", "fhir");
        if (!Directory.Exists(dataDir))
            return NotFound(new { error = "Sample data directory not found.", path = dataDir });

        var result = await _seedService.SeedAllAsync(dataDir, ct);
        return Ok(result);
    }
}

public sealed class SeedFileRequest
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
}
