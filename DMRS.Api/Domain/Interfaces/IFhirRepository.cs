using Hl7.Fhir.Model;
using DMRS.Api.Application.Interfaces;

namespace DMRS.Api.Domain.Interfaces
{
    public interface IFhirRepository
    {
        Task<T?> GetAsync<T>(string id) where T : Resource;
        Task<T?> GetVersionAsync<T>(string id, int versionId) where T : Resource;
        Task<List<T>> GetHistoryAsync<T>(string id) where T : Resource;
        Task<string> CreateAsync<T>(T resource, ISearchIndexer searchIndexer) where T : Resource;
        System.Threading.Tasks.Task UpdateAsync<T>(string id, T resource, ISearchIndexer searchIndexer) where T : Resource;
        System.Threading.Tasks.Task DeleteAsync(string resourceType, string id);
        Task<List<T>> SearchAsync<T>(Dictionary<string, string> queryParams) where T : Resource;

        /// <summary>
        /// Returns the raw database count of matching resources without deserialising them.
        /// Use this in security-critical paths to detect records silently skipped by SearchAsync
        /// due to deserialization errors, and fail closed when the counts diverge.
        /// </summary>
        Task<int> SearchCountAsync<T>(Dictionary<string, string> queryParams) where T : Resource;

        /// <summary>
        /// Returns the number of non-deleted resources of a given type with a single SQL COUNT —
        /// no resources are loaded or deserialized. Used by dashboard/analytics tiles that only
        /// need a total, not the resources themselves.
        /// </summary>
        Task<int> CountByTypeAsync(string resourceType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns a map of patient id → number of non-deleted resources of the given type that
        /// reference that patient, computed with a single grouped COUNT over the search index
        /// (no resources are deserialized). Used to score the whole cohort's risk in one pass.
        /// </summary>
        Task<Dictionary<string, int>> CountByPatientAsync(string resourceType, CancellationToken cancellationToken = default);
    }
}
