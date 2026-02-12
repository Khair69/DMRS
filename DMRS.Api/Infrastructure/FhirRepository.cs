using DMRS.Api.Domain;
using DMRS.Api.Domain.Interfaces;
using DMRS.Api.Infrastructure.Persistence;
using Hl7.Fhir.Model;
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

            // 4. Extract Search Parameters (The hard part - simplified here)
            // You would typically use a strategy pattern to extract fields like "Name" or "BirthDate"
            // based on the T resource type and save them to the ResourceIndex table.

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

        public System.Threading.Tasks.Task UpdateAsync<T>(string id, T resource) where T : Resource
        {
            throw new NotImplementedException();
        }
    }
}
