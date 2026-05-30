using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using DMRS.Api.Infrastructure.Persistence;
using DMRS.Api.Infrastructure.Search.Administrative;
using DMRS.Api.Infrastructure.Search.Clinical;
using DMRS.Api.Infrastructure.Search.Medication;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DMRS.Api.Application.DevTools;

/// <summary>
/// Summary returned by the seed endpoint.
/// </summary>
public sealed class SeedResult
{
    public int FilesProcessed { get; set; }
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public Dictionary<string, int> ByResourceType { get; set; } = new();
}

/// <summary>
/// Current seeding status — returned by the status endpoint.
/// </summary>
public sealed class SeedStatusResult
{
    public int PatientCount { get; set; }
    public bool HasData => PatientCount > 0;
}

/// <summary>
/// Dev-only service that reads Synthea-generated FHIR transaction bundles from a
/// directory and imports the clinically relevant resources into the database.
///
/// Key design decisions:
///   • Parses each bundle entry-by-entry using System.Text.Json so that one
///     R4-format resource (e.g. CarePlan.addresses) doesn't kill the other 200
///     resources in the same file.
///   • Extracts search indices before touching the DbContext so a buggy indexer
///     can't corrupt a partially-written batch.
///   • Idempotent — skips (ResourceType, Id) pairs that already exist.
/// </summary>
public sealed class SeedDataService
{
    // Only these resource types are imported; Claim, CarePlan, ExplanationOfBenefit,
    // DiagnosticReport, etc. are silently skipped.
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Patient",
        "Encounter",
        "Condition",
        "MedicationRequest",
        "Observation",
        "Procedure",
        "AllergyIntolerance"
    };

    private readonly AppDbContext _db;
    private readonly FhirJsonSerializer _serializer;
    private readonly FhirJsonDeserializer _deserializer;
    private readonly IReadOnlyDictionary<string, ISearchIndexer> _indexers;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(
        AppDbContext db,
        FhirJsonSerializer serializer,
        FhirJsonDeserializer deserializer,
        PatientIndexer patientIndexer,
        EncounterIndexer encounterIndexer,
        ConditionIndexer conditionIndexer,
        MedicationRequestIndexer medicationRequestIndexer,
        ObservationIndexer observationIndexer,
        ProcedureIndexer procedureIndexer,
        AllergyIntoleranceIndexer allergyIntoleranceIndexer,
        ILogger<SeedDataService> logger)
    {
        _db = db;
        _serializer = serializer;
        _deserializer = deserializer;
        _logger = logger;

        _indexers = new Dictionary<string, ISearchIndexer>(StringComparer.OrdinalIgnoreCase)
        {
            ["Patient"]            = patientIndexer,
            ["Encounter"]          = encounterIndexer,
            ["Condition"]          = conditionIndexer,
            ["MedicationRequest"]  = medicationRequestIndexer,
            ["Observation"]        = observationIndexer,
            ["Procedure"]          = procedureIndexer,
            ["AllergyIntolerance"] = allergyIntoleranceIndexer
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds resources that are stored in the DB but have no ResourceIndex rows
    /// (saved during a broken seed run) and re-extracts their search indices.
    /// </summary>
    /// <summary>
    /// Returns a count of un-indexed resources per resource type so the client
    /// can decide which types need repair and display progress.
    /// </summary>
    public async Task<Dictionary<string, int>> GetReindexStatusAsync(CancellationToken ct = default)
    {
        var supportedList = SupportedTypes.ToArray();

        var counts = await (
            from r in _db.FhirResources
            where supportedList.Contains(r.ResourceType) && !r.IsDeleted
            where !_db.ResourceIndices.Any(i => i.ResourceType == r.ResourceType && i.ResourceId == r.Id)
            group r by r.ResourceType into g
            select new { g.Key, Count = g.Count() }
        ).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        _logger.LogInformation(
            "Reindex status: {Summary}",
            string.Join(", ", counts.Select(kv => $"{kv.Key}={kv.Value}")));

        return counts;
    }

    /// <summary>
    /// Re-indexes all un-indexed resources of a single resource type.
    /// Called once per type so each HTTP request stays well within browser limits.
    /// Loads content in batches of 100 to avoid OOM on large collections.
    /// </summary>
    public async Task<int> ReindexResourceTypeAsync(
        string resourceType,
        CancellationToken ct = default)
    {
        if (!_indexers.TryGetValue(resourceType, out var indexer))
            return 0;

        // Step 1 — find IDs that need repair (no RawContent loaded yet).
        var toRepairIds = await (
            from r in _db.FhirResources
            where r.ResourceType == resourceType && !r.IsDeleted
            where !_db.ResourceIndices.Any(i => i.ResourceType == r.ResourceType && i.ResourceId == r.Id)
            select r.Id
        ).ToListAsync(ct);

        _logger.LogInformation(
            "Reindex {ResourceType}: found {Count} resources with no index entries",
            resourceType, toRepairIds.Count);

        if (toRepairIds.Count == 0) return 0;

        var repaired   = 0;
        const int BatchSize = 100;

        for (var i = 0; i < toRepairIds.Count; i += BatchSize)
        {
            if (ct.IsCancellationRequested) break;

            var batchIds = toRepairIds.Skip(i).Take(BatchSize).ToList();

            // Step 2 — load content only for this batch.
            var rows = await _db.FhirResources
                .Where(r => r.ResourceType == resourceType && batchIds.Contains(r.Id))
                .Select(r => new { r.Id, r.RawContent })
                .ToListAsync(ct);

            var batchIndices = new List<ResourceIndex>();

            foreach (var row in rows)
            {
                Resource resource;
                try { resource = _deserializer.Deserialize<Resource>(row.RawContent); }
                catch { continue; }

                List<ResourceIndex> indices;
                try
                {
                    indices = indexer.Extract(resource);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Reindex: indexer still failing for {ResourceType}/{Id}", resourceType, row.Id);
                    continue;
                }

                batchIndices.AddRange(indices);
                repaired++;
            }

            if (batchIndices.Count > 0)
            {
                _db.ResourceIndices.AddRange(batchIndices);
                await _db.SaveChangesAsync(ct);
            }
        }

        _logger.LogInformation("Reindex {ResourceType}: repaired {Count}", resourceType, repaired);
        return repaired;
    }

    /// <summary>
    /// Seeds every file in one call. Prefer <see cref="SeedSingleFileAsync"/>
    /// from the HTTP layer so each request stays within browser timeout limits.
    /// </summary>
    public async Task<SeedResult> SeedAllAsync(
        string dataDirectory,
        CancellationToken ct = default)
    {
        var result = new SeedResult();

        var existing = await _db.FhirResources
            .Where(r => SupportedTypes.Contains(r.ResourceType))
            .Select(r => r.ResourceType + "/" + r.Id)
            .ToHashSetAsync(ct);

        var files = Directory.GetFiles(dataDirectory, "*.json");
        result.FilesProcessed = files.Length;

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                await ImportEntriesAsync(json, existing, result, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing seed file {File}", Path.GetFileName(file));
                result.Errors++;
            }
        }

        return result;
    }

    /// <summary>
    /// Seeds a single named file. Called once per HTTP request from the
    /// file-by-file client loop — keeps each request fast.
    /// </summary>
    public async Task<SeedResult> SeedSingleFileAsync(
        string filePath,
        CancellationToken ct = default)
    {
        var result = new SeedResult { FilesProcessed = 1 };

        string json;
        try
        {
            json = await File.ReadAllTextAsync(filePath, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cannot read seed file {File}", Path.GetFileName(filePath));
            result.Errors++;
            return result;
        }

        // Pre-scan candidate IDs without full Firely deserialization, then do
        // a single DB query to check which ones already exist.
        var candidateKeys = ScanCandidateKeys(json);

        var existing = candidateKeys.Count > 0
            ? await _db.FhirResources
                .Where(r => SupportedTypes.Contains(r.ResourceType)
                    && candidateKeys.Contains(r.ResourceType + "/" + r.Id))
                .Select(r => r.ResourceType + "/" + r.Id)
                .ToHashSetAsync(ct)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await ImportEntriesAsync(json, existing, result, ct);
        return result;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lightweight System.Text.Json scan to collect (ResourceType/Id) keys
    /// for supported types — no Firely deserialization, no failures.
    /// </summary>
    private static HashSet<string> ScanCandidateKeys(string bundleJson)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(bundleJson);
            if (!doc.RootElement.TryGetProperty("entry", out var entries)) return keys;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("resource", out var res)) continue;
                if (!res.TryGetProperty("resourceType", out var rtEl)) continue;
                if (!res.TryGetProperty("id", out var idEl)) continue;

                var rt = rtEl.GetString() ?? string.Empty;
                var id = idEl.GetString() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(rt) && !string.IsNullOrWhiteSpace(id)
                    && SupportedTypes.Contains(rt))
                {
                    keys.Add($"{rt}/{id}");
                }
            }
        }
        catch
        {
            // Malformed JSON — let ImportEntriesAsync handle the failure message.
        }
        return keys;
    }

    /// <summary>
    /// Core import loop. Uses System.Text.Json to iterate entries so a single
    /// R4-format resource (e.g. CarePlan, Coverage) does not abort the whole
    /// file. Each supported resource is then individually deserialized by Firely.
    /// </summary>
    private async System.Threading.Tasks.Task ImportEntriesAsync(
        string bundleJson,
        HashSet<string> existing,
        SeedResult result,
        CancellationToken ct)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(bundleJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Malformed JSON in seed file — skipping");
            result.Errors++;
            return;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("entry", out var entries)
                || entries.ValueKind != JsonValueKind.Array)
                return;

            var now        = DateTimeOffset.UtcNow;
            var batchCount = 0;

            foreach (var entry in entries.EnumerateArray())
            {
                if (ct.IsCancellationRequested) break;
                if (!entry.TryGetProperty("resource", out var resourceEl)) continue;
                if (!resourceEl.TryGetProperty("resourceType", out var rtEl)) continue;

                var resourceType = rtEl.GetString() ?? string.Empty;
                if (!SupportedTypes.Contains(resourceType)) continue;
                if (!_indexers.TryGetValue(resourceType, out var indexer)) continue;

                // Read ID directly from JSON — no Firely yet.
                if (!resourceEl.TryGetProperty("id", out var idEl)) continue;
                var id = idEl.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id)) continue;

                var key = $"{resourceType}/{id}";
                if (existing.Contains(key))
                {
                    result.Skipped++;
                    continue;
                }

                // Reserve the key immediately so in-batch duplicates are skipped.
                existing.Add(key);

                // ── Deserialize just this one resource ───────────────────────
                Resource resource;
                try
                {
                    resource = _deserializer.Deserialize<Resource>(resourceEl.GetRawText());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Skipping {ResourceType}/{Id} — {ExceptionType}: {ExceptionMessage}",
                        resourceType, id,
                        ex.GetType().Name,
                        ex.Message.Split('\n')[0]); // first line only, keeps log readable
                    result.Errors++;
                    continue;
                }

                // ── Extract indices before touching the DbContext ────────────
                // If the indexer throws (e.g. null dereference), we skip this
                // resource cleanly without leaving a half-written batch.
                List<ResourceIndex> indices;
                try
                {
                    indices = indexer.Extract(resource);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Indexer failed for {ResourceType}/{Id} — resource stored without indices",
                        resourceType, id);
                    indices = [];
                }

                // ── Persist ──────────────────────────────────────────────────
                try
                {
                    var rawJson = _serializer.SerializeToString(resource);

                    _db.FhirResources.Add(new FhirResource
                    {
                        Id           = id,
                        ResourceType = resourceType,
                        VersionId    = 1,
                        LastUpdated  = now,
                        IsDeleted    = false,
                        RawContent   = rawJson
                    });

                    _db.FhirResourceVersions.Add(new FhirResourceVersion
                    {
                        Id           = id,
                        ResourceType = resourceType,
                        VersionId    = 1,
                        LastUpdated  = now,
                        RawContent   = rawJson
                    });

                    _db.ResourceIndices.AddRange(indices);

                    result.Imported++;
                    result.ByResourceType[resourceType] =
                        result.ByResourceType.GetValueOrDefault(resourceType) + 1;
                    batchCount++;

                    if (batchCount >= 100)
                    {
                        await _db.SaveChangesAsync(ct);
                        batchCount = 0;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DB persist failed for {ResourceType}/{Id}",
                        resourceType, id);
                    result.Errors++;
                    _db.ChangeTracker.Clear();
                    batchCount = 0;
                }
            }

            if (batchCount > 0)
            {
                try
                {
                    await _db.SaveChangesAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to flush final batch");
                    result.Errors   += batchCount;
                    result.Imported -= batchCount;
                    _db.ChangeTracker.Clear();
                }
            }
        }
    }
}
