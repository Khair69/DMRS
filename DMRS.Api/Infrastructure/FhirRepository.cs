using DMRS.Api.Application.Interfaces;
using DMRS.Api.Domain;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Persistence;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Microsoft.EntityFrameworkCore;

namespace DMRS.Api.Infrastructure
{
    public class FhirRepository : IFhirRepository
    {
        private readonly AppDbContext _context;
        private readonly FhirJsonSerializer _serializer;
        private readonly FhirJsonDeserializer _deserializer;
        public FhirRepository(AppDbContext context, FhirJsonSerializer serializer, FhirJsonDeserializer deserializer)
        {
            _context = context;
            _serializer = serializer;
            _deserializer = deserializer;
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

        public async Task<T> GetAsync<T>(string id) where T : Resource
        {
            var entity = await _context.FhirResources
                        .FirstOrDefaultAsync(r => r.Id == id && r.ResourceType == typeof(T).Name && !r.IsDeleted);

            if (entity == null) return null;

            // Deserialize back to FHIR Object
            return _deserializer.Deserialize<T>(entity.RawContent);
        }

        public async Task<List<T>> SearchAsync<T>(string searchParam, string value) where T : Resource
        {
            var typeName = typeof(T).Name;

            // 1. Find IDs in the Index Table
            var matches = await _context.ResourceIndices
                .Where(x => x.ResourceType == typeName
                         && x.SearchParamCode == searchParam
                         && x.Value == value.ToLower())
                .Select(x => x.ResourceId)
                .ToListAsync();

            // 2. Load the actual JSON for those IDs
            var resources = await _context.FhirResources
                .Where(r => r.ResourceType == typeName && matches.Contains(r.Id))
                .ToListAsync();

            // 3. Deserialize
            return resources.Select(r => _deserializer.Deserialize<T>(r.RawContent)).ToList();
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

            return _deserializer.Deserialize<T>(entity.RawContent);
        }

        public async Task<List<T>> GetHistoryAsync<T>(string id) where T : Resource
        {
            var resourceType = typeof(T).Name;

            var versions = await _context.FhirResourceVersions
                .Where(r => r.ResourceType == resourceType && r.Id == id)
                .OrderByDescending(r => r.VersionId)
                .ToListAsync();

            return versions
                .Select(v => _deserializer.Deserialize<T>(v.RawContent))
                .ToList();
        }
    }
}

