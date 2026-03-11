using DMRS.Client.Features.AllergyIntolerances.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.AllergyIntolerances.Services;

public sealed class AllergyIntoleranceFeatureService : FhirFeatureServiceBase<AllergyIntolerance, AllergyIntoleranceEditModel, AllergyIntoleranceSummaryViewModel>
{
    public AllergyIntoleranceFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override AllergyIntolerance ToResource(AllergyIntoleranceEditModel model)
        => model.ToFhirAllergy();

    protected override AllergyIntoleranceSummaryViewModel MapToSummary(AllergyIntolerance allergy)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(allergy.Patient?.Reference, "patient") ?? "(unknown)";
        var codeText = allergy.Code?.Text ?? allergy.Code?.Coding.FirstOrDefault()?.Code ?? "(no-code)";
        var clinicalStatus = allergy.ClinicalStatus?.Coding.FirstOrDefault()?.Code ?? allergy.ClinicalStatus?.Text;

        return new AllergyIntoleranceSummaryViewModel(
            allergy.Id ?? "(no-id)",
            patientId,
            codeText,
            clinicalStatus);
    }
}
