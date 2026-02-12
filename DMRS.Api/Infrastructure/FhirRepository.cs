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
        private readonly ISearchIndexer _searchIndexer;
        public FhirRepository(AppDbContext context, FhirJsonSerializer serializer, FhirJsonDeserializer deserializer, ISearchIndexer searchIndexer)
        {
            _context = context;
            _serializer = serializer;
            _deserializer = deserializer;
            _searchIndexer = searchIndexer;
        }

        public async Task<string> CreateAsync<T>(T resource) where T : Resource
        {
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

            var indices = _searchIndexer.Extract(resource);
            _context.ResourceIndices.AddRange(indices);

            await _context.SaveChangesAsync();
            return resource.Id;
        }

        public System.Threading.Tasks.Task DeleteAsync(string resourceType, string id)
        {
            throw new NotImplementedException();
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
            var typeName = typeof(T).Name; // "Patient"

            // 1. Find IDs in the Index Table
            var matches = await _context.ResourceIndices
                .Where(x => x.ResourceType == typeName
                         && x.SearchParamCode == searchParam
                         && x.Value == value.ToLower()) // FHIR search is case-insensitive
                .Select(x => x.ResourceId)
                .ToListAsync();

            // 2. Load the actual JSON for those IDs
            var resources = await _context.FhirResources
                .Where(r => r.ResourceType == typeName && matches.Contains(r.Id))
                .ToListAsync();

            // 3. Deserialize
            return resources.Select(r => _deserializer.Deserialize<T>(r.RawContent)).ToList();
        }

        public System.Threading.Tasks.Task UpdateAsync<T>(string id, T resource) where T : Resource
        {
            throw new NotImplementedException();
        }
    }
}
