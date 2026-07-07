using DMRS.Client.Features.Cds.Models;
using DMRS.Client.Features.MedicationRequests.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DMRS.Client.Features.MedicationRequests.Services;

public sealed class MedicationDecisionSupportService
{
    private readonly FhirApiService _fhirApiService;
    private readonly FhirJsonSerializer _serializer;
    private readonly ILogger<MedicationDecisionSupportService> _logger;

    public MedicationDecisionSupportService(
        FhirApiService fhirApiService,
        FhirJsonSerializer serializer,
        ILogger<MedicationDecisionSupportService> logger)
    {
        _fhirApiService = fhirApiService;
        _serializer = serializer;
        _logger = logger;
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
            catch (Exception ex)
            {
                // Log so the failure is visible, then fall through to the text-based search.
                _logger.LogWarning(ex, "RxCUI medication lookup failed for {RxCui}; falling back to text search.", rxCui);
            }
        }

        if (medicine is null && !string.IsNullOrWhiteSpace(text))
        {
            // Wrap the fallback in the same try/catch so both lookup paths behave consistently.
            try
            {
                var searchResult = await _fhirApiService.GetApiJsonAsync<List<CdsMedicineKnowledgeModel>>(
                    $"cds/medications?q={Uri.EscapeDataString(text)}");
                medicine = searchResult?.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Text-based medication search failed for query '{Text}'.", text);
            }
        }

        var hookResponse = await EvaluateHookAsync(request, patientId);

        return new MedicationDecisionSupportSnapshotModel
        {
            Medicine = medicine,
            HookResponse = hookResponse
        };
    }

    /// <summary>
    /// Runs only the medication-prescribe CDS hook and returns its cards — no medicine-knowledge
    /// lookup. Used to surface live rule violations for a patient's existing medications without the
    /// extra knowledge round-trips that <see cref="GetSnapshotAsync"/> makes for its display panel.
    /// </summary>
    public async Task<IReadOnlyList<CdsCardModel>> EvaluateCardsAsync(MedicationRequest request, string patientId)
    {
        var response = await EvaluateHookAsync(request, patientId);
        return response?.Cards ?? [];
    }

    private async Task<CdsHookResponseModel?> EvaluateHookAsync(MedicationRequest request, string patientId)
    {
        // Serialize the FHIR resource with the FHIR-aware serializer before embedding it in the
        // hook payload. JsonContent.Create uses System.Text.Json which cannot handle Firely SDK
        // polymorphic types (e.g. Dosage.DoseAndRateComponent.Dose declared as DataType) and
        // would silently strip dose/unit data, causing dose-threshold CDS rules to never fire.
        using var requestDoc = JsonDocument.Parse(_serializer.SerializeToString(request));

        return await _fhirApiService.PostApiJsonAsync<object, CdsHookResponseModel>(
            "cds-services/medication-prescribe",
            new
            {
                hookInstance = Guid.NewGuid().ToString("N"),
                context = new
                {
                    patientId,
                    medicationRequest = requestDoc.RootElement
                }
            });
    }
}
