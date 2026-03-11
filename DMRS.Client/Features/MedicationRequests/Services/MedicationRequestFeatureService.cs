using DMRS.Client.Features.MedicationRequests.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.MedicationRequests.Services;

public sealed class MedicationRequestFeatureService : FhirFeatureServiceBase<MedicationRequest, MedicationRequestEditModel, MedicationRequestSummaryViewModel>
{
    public MedicationRequestFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override MedicationRequest ToResource(MedicationRequestEditModel model)
        => model.ToFhirMedicationRequest();

    protected override MedicationRequestSummaryViewModel MapToSummary(MedicationRequest request)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(request.Subject?.Reference, "patient") ?? "(unknown)";
        var medicationConcept = request.Medication?.Concept;
        var medicationText = medicationConcept?.Text ?? medicationConcept?.Coding.FirstOrDefault()?.Code ?? "(no-med)";

        return new MedicationRequestSummaryViewModel(
            request.Id ?? "(no-id)",
            patientId,
            medicationText,
            request.Status?.ToString() ?? "unknown",
            request.Intent?.ToString() ?? "unknown");
    }
}
