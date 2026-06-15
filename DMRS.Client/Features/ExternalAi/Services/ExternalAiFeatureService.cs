using DMRS.Client.Features.ExternalAi.Models;
using DMRS.Client.Services;

namespace DMRS.Client.Features.ExternalAi.Services;

/// <summary>
/// Client gateway for the external ("away") AI model feature: admin CRUD over the registry plus
/// running a model against a patient.
/// </summary>
public sealed class ExternalAiFeatureService
{
    private const string ModelsPath = "external-ai/models";

    private readonly FhirApiService _api;

    public ExternalAiFeatureService(FhirApiService api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<ExternalAiModelDto>> ListModelsAsync()
        => await _api.GetApiJsonAsync<List<ExternalAiModelDto>>(ModelsPath) ?? [];

    public Task<ExternalAiModelDto?> GetModelAsync(Guid id)
        => _api.GetApiJsonAsync<ExternalAiModelDto>($"{ModelsPath}/{id}");

    public Task<ExternalAiModelDto?> CreateModelAsync(ExternalAiModelInput input)
        => _api.PostApiJsonAsync<ExternalAiModelInput, ExternalAiModelDto>(ModelsPath, input);

    public Task<ExternalAiModelDto?> UpdateModelAsync(Guid id, ExternalAiModelInput input)
        => _api.PutApiJsonAsync<ExternalAiModelInput, ExternalAiModelDto>($"{ModelsPath}/{id}", input);

    public Task DeleteModelAsync(Guid id)
        => _api.DeleteApiAsync($"{ModelsPath}/{id}");

    /// <summary>Returns only the active models — used to populate the "run" picker.</summary>
    public async Task<IReadOnlyList<ExternalAiModelDto>> ListActiveModelsAsync()
        => (await ListModelsAsync()).Where(m => m.IsActive).ToList();

    public Task<ExternalAiInferenceResult?> RunAsync(Guid modelId, string patientId)
        => _api.PostApiJsonAsync<object?, ExternalAiInferenceResult>($"external-ai/infer/{modelId}/{patientId}", null);
}
