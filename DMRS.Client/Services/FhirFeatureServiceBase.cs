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
