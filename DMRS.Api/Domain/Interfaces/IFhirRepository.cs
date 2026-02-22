using Hl7.Fhir.Model;
using DMRS.Api.Application.Interfaces;

namespace DMRS.Api.Domain.Interfaces
{
    public interface IFhirRepository
    {
        Task<T> GetAsync<T>(string id) where T : Resource;
        Task<T?> GetVersionAsync<T>(string id, int versionId) where T : Resource;
        Task<List<T>> GetHistoryAsync<T>(string id) where T : Resource;
        Task<string> CreateAsync<T>(T resource, ISearchIndexer searchIndexer) where T : Resource;
        System.Threading.Tasks.Task UpdateAsync<T>(string id, T resource, ISearchIndexer searchIndexer) where T : Resource;
        System.Threading.Tasks.Task DeleteAsync(string resourceType, string id);
        Task<List<T>> SearchAsync<T>(string searchParams,string value) where T : Resource;
    }
}
