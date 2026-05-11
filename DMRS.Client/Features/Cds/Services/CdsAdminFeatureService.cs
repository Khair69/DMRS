using System.Web;
using DMRS.Client.Features.Cds.Models;
using DMRS.Client.Services;

namespace DMRS.Client.Features.Cds.Services;

public sealed class CdsAdminFeatureService
{
    private readonly FhirApiService _fhirApiService;

    public CdsAdminFeatureService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    public async Task<IReadOnlyList<CdsRuleSummary>> ListRulesAsync()
        => await _fhirApiService.GetApiJsonAsync<List<CdsRuleSummary>>("cds/rules") ?? [];

    public async Task<IReadOnlyList<CdsVariableDefinitionModel>> ListVariablesAsync()
        => await _fhirApiService.GetApiJsonAsync<List<CdsVariableDefinitionModel>>("cds/rules/variables") ?? [];

    public async Task<IReadOnlyList<CdsRuleTemplateDefinitionModel>> ListTemplatesAsync()
        => await _fhirApiService.GetApiJsonAsync<List<CdsRuleTemplateDefinitionModel>>("cds/rules/templates") ?? [];

    public async Task<CdsRuleSummary?> CompileTemplateAsync(CdsRuleTemplateRequestModel request)
        => await _fhirApiService.PostApiJsonAsync<CdsRuleTemplateRequestModel, CdsRuleSummary>("cds/rules/templates/compile", request);

    public async Task<CdsRuleSummary?> CreateFromTemplateAsync(CdsRuleTemplateRequestModel request)
        => await _fhirApiService.PostApiJsonAsync<CdsRuleTemplateRequestModel, CdsRuleSummary>("cds/rules/templates", request);

    public async Task<CdsRuleSummary?> CreateRuleAsync(CdsRuleSummary rule)
        => await _fhirApiService.PostApiJsonAsync<CdsRuleSummary, CdsRuleSummary>("cds/rules", rule);

    public async Task<CdsRuleSummary?> UpdateRuleAsync(CdsRuleSummary rule)
        => await _fhirApiService.PutApiJsonAsync<CdsRuleSummary, CdsRuleSummary>($"cds/rules/{rule.Id}", rule);

    public async Task SetRuleActiveAsync(Guid id, bool isActive)
        => await _fhirApiService.PatchApiAsync($"cds/rules/{id}/{(isActive ? "activate" : "deactivate")}");

    public async Task<CdsRuleValidationResultModel?> ValidateRuleAsync(CdsRuleSummary rule)
        => await _fhirApiService.PostApiJsonAsync<CdsRuleSummary, CdsRuleValidationResultModel>("cds/rules/validate", rule);

    public async Task<CdsRulePreviewResponseModel?> PreviewRuleAsync(CdsRulePreviewRequestModel request)
        => await _fhirApiService.PostApiJsonAsync<CdsRulePreviewRequestModel, CdsRulePreviewResponseModel>("cds/rules/preview", request);

    public async Task<IReadOnlyList<CdsMedicineKnowledgeModel>> SearchMedicinesAsync(string? query)
    {
        var path = string.IsNullOrWhiteSpace(query)
            ? "cds/medications"
            : $"cds/medications?q={HttpUtility.UrlEncode(query)}";

        return await _fhirApiService.GetApiJsonAsync<List<CdsMedicineKnowledgeModel>>(path) ?? [];
    }
}
