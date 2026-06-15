using Hl7.Fhir.Model;

namespace DMRS.Client.Services;

public abstract class FhirFeatureServiceBase<TResource, TEditModel, TSummary>
    where TResource : Resource
{
    private readonly FhirApiService _fhirApiService;

    protected FhirFeatureServiceBase(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    public async Task<IReadOnlyList<TSummary>> SearchAsync(string searchParam, string value)
    {
        var resources = await _fhirApiService.SearchAsync<TResource>(searchParam, value);
        return resources.Select(MapToSummary).ToList();
    }

    // Capped list used to populate Index pages on open. Passing a count sends FHIR _count so the API
    // loads/deserializes only the most-recent N rows instead of the whole collection. The API also
    // access-filters server-side (FhirBaseController.FilterReadableAsync), so this only ever returns
    // resources the caller may read.
    public async Task<IReadOnlyList<TSummary>> ListAsync(int? count = null, Dictionary<string, string>? extraParams = null)
    {
        var query = extraParams is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(extraParams);

        if (count is > 0)
        {
            query["_count"] = count.Value.ToString();
        }

        var resources = await _fhirApiService.SearchResourcesAsync<TResource>(query.Count == 0 ? null : query);
        return resources.Select(MapToSummary).ToList();
    }

    public Task<TResource?> GetAsync(string id)
    {
        return _fhirApiService.GetResourceAsync<TResource>(id);
    }

    public Task<IReadOnlyList<TResource>> GetHistoryAsync(string id)
    {
        return _fhirApiService.GetHistoryAsync<TResource>(id);
    }

    public async Task<TResource?> CreateAsync(TEditModel model)
    {
        var resource = ToResource(model);
        resource.Id = null;
        return await _fhirApiService.CreateResourceAsync(resource);
    }

    public async Task<TResource?> UpdateAsync(string id, TEditModel model)
    {
        var resource = ToResource(model);
        resource.Id = id;
        return await _fhirApiService.UpdateResourceAsync(id, resource);
    }

    public System.Threading.Tasks.Task DeleteAsync(string id)
    {
        return _fhirApiService.DeleteResourceAsync<TResource>(id);
    }

    protected abstract TResource ToResource(TEditModel model);
    protected abstract TSummary MapToSummary(TResource resource);
}
