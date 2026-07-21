using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Persistence;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DMRS.Api.Infrastructure
{
    public class FhirRepository : IFhirRepository
    {
        private readonly AppDbContext _context;
        private readonly FhirJsonSerializer _serializer;
        private readonly FhirJsonDeserializer _deserializer;
        private readonly ILogger<FhirRepository> _logger;

        // FHIR "string"-type search params (names, addresses) match on a case-insensitive substring,
        // so typing "a" finds every value containing "a". Token/reference/date params (gender,
        // identifier, organization, birthdate, …) are NOT listed here and keep exact matching, where a
        // partial match would be wrong. Index values are stored lowercased and the query value is
        // lowercased too, so the Contains comparison is case-insensitive even on PostgreSQL.
        private static readonly HashSet<string> PartialMatchParams = new(StringComparer.Ordinal)
        {
            "name", "family", "given",
            "address", "address-city", "address-state", "address-country", "address-postalcode",
        };

        // Applies one search param to the index query: substring match for string-type params,
        // exact match for everything else. Shared by SearchAsync and SearchCountAsync so both agree.
        private static IQueryable<ResourceIndex> ApplyParamFilter(IQueryable<ResourceIndex> query, string key, string value)
        {
            return PartialMatchParams.Contains(key)
                ? query.Where(x => x.SearchParamCode == key && x.Value.Contains(value))
                : query.Where(x => x.SearchParamCode == key && x.Value == value);
        }

        // Resolves the ids of resources matching EVERY non-control search param.
        //
        // Each ResourceIndex row carries exactly one SearchParamCode, so the per-param predicates must
        // NOT be chained onto a single row query: that asks one row to match two different param codes
        // at once, which no row can satisfy, and every multi-param search silently returns nothing.
        // Instead each param resolves its own id set and the sets are intersected — the correct
        // "resource matches all params" semantics. Shared by SearchAsync and SearchCountAsync so both agree.
        private async Task<List<string>> ResolveMatchingIdsAsync(string typeName, Dictionary<string, string> queryParams)
        {
            List<string>? matchedIds = null;

            foreach (var param in queryParams)
            {
                var key = param.Key.ToLower();
                var value = param.Value.ToLower();

                // Ignore control params (_count, _sort, …) — they aren't index filters.
                if (key.StartsWith("_"))
                    continue;

                var paramIds = await ApplyParamFilter(
                        _context.ResourceIndices.Where(x => x.ResourceType == typeName), key, value)
                    .Select(x => x.ResourceId)
                    .Distinct()
                    .ToListAsync();

                matchedIds = matchedIds is null
                    ? paramIds
                    : matchedIds.Intersect(paramIds, StringComparer.Ordinal).ToList();

                // Nothing can match the remaining params either — skip the extra round-trips.
                if (matchedIds.Count == 0)
                    break;
            }

            return matchedIds ?? [];
        }

        public FhirRepository(AppDbContext context, FhirJsonSerializer serializer, FhirJsonDeserializer deserializer, ILogger<FhirRepository> logger)
        {
            _context = context;
            _serializer = serializer;
            _deserializer = deserializer;
            _logger = logger;
        }

        public async Task<string> CreateAsync<T>(T resource, ISearchIndexer searchIndexer) where T : Resource
        {
            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(searchIndexer);

            // 1. Generate ID if missing
            if (string.IsNullOrEmpty(resource.Id))
                resource.Id = Guid.NewGuid().ToString();

            // 2. Serialize to JSON
            var json = _serializer.SerializeToString(resource);

            // 3. Map to Database Entity
            var dbEntity = new FhirResource
            {
                Id = resource.Id,
                ResourceType = resource.TypeName,
                VersionId = 1,
                LastUpdated = DateTimeOffset.UtcNow,
                IsDeleted = false,
                RawContent = json
            };

            _context.FhirResources.Add(dbEntity);
            _context.FhirResourceVersions.Add(new FhirResourceVersion
            {
                Id = resource.Id,
                ResourceType = resource.TypeName,
                VersionId = dbEntity.VersionId,
                LastUpdated = dbEntity.LastUpdated,
                RawContent = json
            });

            var indices = searchIndexer.Extract(resource);
            _context.ResourceIndices.AddRange(indices);

            await _context.SaveChangesAsync();
            return resource.Id;
        }

        public async System.Threading.Tasks.Task DeleteAsync(string resourceType, string id)
        {
            if (string.IsNullOrWhiteSpace(resourceType))
                throw new ArgumentException("Resource type is required.", nameof(resourceType));

            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Resource id is required.", nameof(id));

            var existing = await _context.FhirResources
                .FirstOrDefaultAsync(r => r.ResourceType == resourceType && r.Id == id && !r.IsDeleted);

            if (existing == null)
                throw new KeyNotFoundException($"Resource '{resourceType}/{id}' not found.");

            var utcNow = DateTimeOffset.UtcNow;
            existing.IsDeleted = true;
            existing.LastUpdated = utcNow;
            existing.VersionId += 1;

            _context.FhirResourceVersions.Add(new FhirResourceVersion
            {
                Id = existing.Id,
                ResourceType = existing.ResourceType,
                VersionId = existing.VersionId,
                LastUpdated = existing.LastUpdated,
                RawContent = existing.RawContent
            });

            await _context.SaveChangesAsync();

            await _context.ResourceIndices
                .Where(i => i.ResourceType == resourceType && i.ResourceId == id)
                .ExecuteDeleteAsync();

        }

        public async Task<T?> GetAsync<T>(string id) where T : Resource
        {
            var entity = await _context.FhirResources
                        .FirstOrDefaultAsync(r => r.Id == id && r.ResourceType == typeof(T).Name && !r.IsDeleted);

            if (entity == null) return null;

            T resource;
            try
            {
                resource = _deserializer.Deserialize<T>(entity.RawContent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping malformed FHIR resource {ResourceType}/{ResourceId} during read.", entity.ResourceType, entity.Id);
                return null;
            }

            resource.Meta ??= new Meta();
            resource.Meta.VersionId = entity.VersionId.ToString();
            resource.Meta.LastUpdated = entity.LastUpdated;

            return resource;
        }

        public async Task<List<T>> SearchAsync<T>(Dictionary<string, string> queryParams)
            where T : Resource
        {
            var typeName = typeof(T).Name;
            var limit = ParseCount(queryParams);
            var hasFilters = queryParams.Any(p => !p.Key.StartsWith("_", StringComparison.Ordinal));

            var resourceQuery = _context.FhirResources
                .Where(r => r.ResourceType == typeName && !r.IsDeleted);

            // Only join the search index when there's an actual filter. An unfiltered list (e.g. an
            // Index page's capped default load) hits the resource table directly, avoiding a full
            // materialization of every index row for the type.
            if (hasFilters)
            {
                var matchedIds = await ResolveMatchingIdsAsync(typeName, queryParams);
                resourceQuery = resourceQuery.Where(r => matchedIds.Contains(r.Id));
            }

            // _count caps the rows we load AND deserialize — the expensive part. Order by most-recent
            // so the cap is a deterministic "latest N" preview rather than an arbitrary slice.
            if (limit is > 0)
            {
                resourceQuery = resourceQuery
                    .OrderByDescending(r => r.LastUpdated)
                    .Take(limit.Value);
            }

            var resources = await resourceQuery.ToListAsync();

            var result = new List<T>(resources.Count);
            foreach (var resourceEntity in resources)
            {
                try
                {
                    var parsed = _deserializer.Deserialize<T>(resourceEntity.RawContent);
                    result.Add(parsed);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipping malformed FHIR resource {ResourceType}/{ResourceId} during search.", resourceEntity.ResourceType, resourceEntity.Id);
                }
            }

            return result;
        }

        public async Task<List<string>> SuggestValuesAsync<T>(string searchParamCode, string prefix, int limit, IReadOnlyCollection<string>? restrictToIds)
            where T : Resource
        {
            var typeName = typeof(T).Name;
            var code = searchParamCode.ToLowerInvariant();
            var value = prefix.ToLowerInvariant();

            var query = _context.ResourceIndices
                .Where(x => x.ResourceType == typeName && x.SearchParamCode == code && x.Value.Contains(value));

            // null = unrestricted (system caller). A non-null set is the exact whitelist of resource ids
            // the caller may read — an empty set yields no suggestions. Mirrors ResolveAccessiblePatientIdsAsync.
            if (restrictToIds is not null)
            {
                var ids = restrictToIds.ToList();
                query = query.Where(x => ids.Contains(x.ResourceId));
            }

            return await query
                .Select(x => x.Value)
                .Distinct()
                .OrderBy(v => v)
                .Take(limit)
                .ToListAsync();
        }

        // Reads the FHIR _count control parameter (case-insensitive); returns null when absent/invalid.
        private static int? ParseCount(Dictionary<string, string> queryParams)
        {
            foreach (var kvp in queryParams)
            {
                if (string.Equals(kvp.Key, "_count", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(kvp.Value, out var n) && n > 0)
                {
                    return n;
                }
            }

            return null;
        }

        public async System.Threading.Tasks.Task UpdateAsync<T>(string id, T resource, ISearchIndexer searchIndexer) where T : Resource
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Resource id is required.", nameof(id));

            ArgumentNullException.ThrowIfNull(resource);
            ArgumentNullException.ThrowIfNull(searchIndexer);

            var resourceType = typeof(T).Name;
            var utcNow = DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(resource.Id) && !string.Equals(resource.Id, id, StringComparison.Ordinal))
                throw new ArgumentException("Body resource id must match route id.", nameof(resource));

            resource.Id = id;

            var existing = await _context.FhirResources
                .FirstOrDefaultAsync(r => r.ResourceType == resourceType && r.Id == id && !r.IsDeleted);

            if (existing == null)
                throw new KeyNotFoundException($"Resource '{resourceType}/{id}' not found.");

            existing.RawContent = _serializer.SerializeToString(resource);
            existing.LastUpdated = utcNow;
            existing.VersionId += 1;

            await _context.ResourceIndices
                .Where(i => i.ResourceType == resourceType && i.ResourceId == id)
                .ExecuteDeleteAsync();

            _context.FhirResourceVersions.Add(new FhirResourceVersion
            {
                Id = existing.Id,
                ResourceType = existing.ResourceType,
                VersionId = existing.VersionId,
                LastUpdated = existing.LastUpdated,
                RawContent = existing.RawContent
            });

            var indices = searchIndexer.Extract(resource);
            if (indices.Count > 0)
            {
                _context.ResourceIndices.AddRange(indices);
            }

            await _context.SaveChangesAsync();

        }

        public async Task<T?> GetVersionAsync<T>(string id, int versionId) where T : Resource
        {
            var resourceType = typeof(T).Name;

            var entity = await _context.FhirResourceVersions
                .FirstOrDefaultAsync(r => r.ResourceType == resourceType && r.Id == id && r.VersionId == versionId);

            if (entity == null)
            {
                return null;
            }

            T resource;
            try
            {
                resource = _deserializer.Deserialize<T>(entity.RawContent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping malformed FHIR resource version {ResourceType}/{ResourceId}/_history/{VersionId}.", entity.ResourceType, entity.Id, entity.VersionId);
                return null;
            }

            resource.Meta ??= new Meta();
            resource.Meta.VersionId = entity.VersionId.ToString();
            resource.Meta.LastUpdated = entity.LastUpdated;

            return resource;
        }

        public Task<int> CountByTypeAsync(string resourceType, CancellationToken cancellationToken = default)
        {
            return _context.FhirResources
                .CountAsync(r => r.ResourceType == resourceType && !r.IsDeleted, cancellationToken);
        }

        public async Task<Dictionary<string, int>> CountByPatientAsync(string resourceType, CancellationToken cancellationToken = default)
        {
            // Each resource contributes exactly one "patient" index row (value: "patient/<id>"),
            // so grouping the index by that value yields the per-patient resource count.
            var rows = await _context.ResourceIndices
                .Where(i => i.ResourceType == resourceType && i.SearchParamCode == "patient")
                .GroupBy(i => i.Value)
                .Select(g => new { Patient = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var id = row.Patient.StartsWith("patient/", StringComparison.OrdinalIgnoreCase)
                    ? row.Patient["patient/".Length..]
                    : row.Patient;
                result[id] = row.Count;
            }

            return result;
        }

        public async Task<int> SearchCountAsync<T>(Dictionary<string, string> queryParams) where T : Resource
        {
            var typeName = typeof(T).Name;
            var hasFilters = queryParams.Any(p => !p.Key.StartsWith("_", StringComparison.Ordinal));

            var query = _context.FhirResources
                .Where(r => r.ResourceType == typeName && !r.IsDeleted);

            // Mirrors SearchAsync: with no filters the count covers every live resource of the type,
            // rather than only those that happen to have index rows.
            if (hasFilters)
            {
                var matchedIds = await ResolveMatchingIdsAsync(typeName, queryParams);
                query = query.Where(r => matchedIds.Contains(r.Id));
            }

            return await query.CountAsync();
        }

        public async Task<List<T>> GetHistoryAsync<T>(string id) where T : Resource
        {
            var resourceType = typeof(T).Name;

            var versions = await _context.FhirResourceVersions
                .Where(r => r.ResourceType == resourceType && r.Id == id)
                .OrderByDescending(r => r.VersionId)
                .ToListAsync();

            var resources = new List<T>(versions.Count);
            var parsedVersions = new List<FhirResourceVersion>(versions.Count);

            foreach (var version in versions)
            {
                try
                {
                    var resource = _deserializer.Deserialize<T>(version.RawContent);
                    resources.Add(resource);
                    parsedVersions.Add(version);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipping malformed FHIR resource version {ResourceType}/{ResourceId}/_history/{VersionId}.", version.ResourceType, version.Id, version.VersionId);
                }
            }

            for (int i = 0;i < resources.Count;i++)
            {
                var resource = resources[i];
                resource.Meta ??= new Meta();
                resource.Meta.VersionId = parsedVersions[i].VersionId.ToString();
                resource.Meta.LastUpdated = parsedVersions[i].LastUpdated;
            }

            return resources;
        }
    }
}

