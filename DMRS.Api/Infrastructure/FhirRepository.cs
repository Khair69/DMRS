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

            var utcNow = DateTimeOffset.UtcNow;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var updated = await _context.FhirResources
                .Where(r => r.ResourceType == resourceType && r.Id == id && !r.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.IsDeleted, true)
                    .SetProperty(r => r.LastUpdated, utcNow)
                    .SetProperty(r => r.VersionId, r => r.VersionId + 1));

            if (updated == 0)
                throw new KeyNotFoundException($"Resource '{resourceType}/{id}' not found.");

            await _context.ResourceIndices
                .Where(i => i.ResourceType == resourceType && i.ResourceId == id)
                .ExecuteDeleteAsync();

            await transaction.CommitAsync();

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

            var updatedJson = _serializer.SerializeToString(resource);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var updated = await _context.FhirResources
                .Where(r => r.ResourceType == resourceType && r.Id == id && !r.IsDeleted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.RawContent, updatedJson)
                    .SetProperty(r => r.LastUpdated, utcNow)
                    .SetProperty(r => r.VersionId, r => r.VersionId + 1));

            if (updated == 0)
                throw new KeyNotFoundException($"Resource '{resourceType}/{id}' not found.");

            await _context.ResourceIndices
                .Where(i => i.ResourceType == resourceType && i.ResourceId == id)
                .ExecuteDeleteAsync();

            var indices = searchIndexer.Extract(resource);
            if (indices.Count > 0)
            {
                _context.ResourceIndices.AddRange(indices);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

        }
    }
}
