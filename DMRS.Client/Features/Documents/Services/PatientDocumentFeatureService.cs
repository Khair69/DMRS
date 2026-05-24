using DMRS.Client.Features.Documents.Models;
using DMRS.Client.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace DMRS.Client.Features.Documents.Services;

public sealed class PatientDocumentFeatureService
{
    private readonly FhirApiService _api;

    public PatientDocumentFeatureService(FhirApiService api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<PatientDocumentModel>> ListAsync(string patientId)
    {
        var records = await _api.GetApiJsonAsync<List<PatientDocumentModel>>(
            $"api/patients/{patientId}/documents");
        return records ?? [];
    }

    public async Task<PatientDocumentModel?> UploadAsync(string patientId, IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();

        var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", file.Name);

        return await _api.PostMultipartAsync<PatientDocumentModel>(
            $"api/patients/{patientId}/documents", content);
    }

    public async Task DeleteAsync(string patientId, string documentId)
    {
        await _api.DeleteApiAsync($"api/patients/{patientId}/documents/{documentId}");
    }

    public string GetDownloadUrl(string patientId, string documentId)
        => _api.GetDownloadUrl($"api/patients/{patientId}/documents/{documentId}/content");
}
