using DMRS.Client.Features.Cds.Models;
using DMRS.Client.Features.MedicationRequests.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.MedicationRequests.Services;

public sealed class MedicationDecisionSupportService
{
    private readonly FhirApiService _fhirApiService;

    public MedicationDecisionSupportService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    public async Task<MedicationDecisionSupportSnapshotModel> GetSnapshotAsync(MedicationRequest request, string patientId)
    {
        var medicationCoding = request.Medication?.Concept?.Coding.FirstOrDefault();
        var rxCui = medicationCoding?.Code;
        var text = request.Medication?.Concept?.Text;

        CdsMedicineKnowledgeModel? medicine = null;

        if (!string.IsNullOrWhiteSpace(rxCui))
        {
            try
            {
                medicine = await _fhirApiService.GetApiJsonAsync<CdsMedicineKnowledgeModel>($"cds/medications/{Uri.EscapeDataString(rxCui)}");
            }
            catch
            {
            }
        }

        if (medicine is null && !string.IsNullOrWhiteSpace(text))
        {
            var searchResult = await _fhirApiService.GetApiJsonAsync<List<CdsMedicineKnowledgeModel>>($"cds/medications?q={Uri.EscapeDataString(text)}");
            medicine = searchResult?.FirstOrDefault();
        }

        var hookResponse = await _fhirApiService.PostApiJsonAsync<object, CdsHookResponseModel>(
            "cds-services/medication-prescribe",
            new
            {
                hookInstance = Guid.NewGuid().ToString("N"),
                context = new
                {
                    patientId,
                    medicationRequest = request
                }
            });

        return new MedicationDecisionSupportSnapshotModel
        {
            Medicine = medicine,
            HookResponse = hookResponse
        };
    }
}
