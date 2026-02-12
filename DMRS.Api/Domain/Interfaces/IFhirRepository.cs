using Hl7.Fhir.Model;
namespace DMRS.Api.Domain.Interfaces
{
    public interface IFhirRepository
    {
        Task<T> GetAsync<T>(string id) where T : Resource;
        Task<string> CreateAsync<T>(T resource) where T : Resource;
        System.Threading.Tasks.Task UpdateAsync<T>(string id, T resource) where T : Resource;
        System.Threading.Tasks.Task DeleteAsync(string resourceType, string id);
    }
}
