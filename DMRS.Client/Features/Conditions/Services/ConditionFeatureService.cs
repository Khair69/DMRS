using DMRS.Client.Features.Conditions.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Conditions.Services;

public sealed class ConditionFeatureService : FhirFeatureServiceBase<Condition, ConditionEditModel, ConditionSummaryViewModel>
{
    public ConditionFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override Condition ToResource(ConditionEditModel model)
        => model.ToFhirCondition();

    protected override ConditionSummaryViewModel MapToSummary(Condition condition)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(condition.Subject?.Reference, "patient") ?? "(unknown)";
        var codeText = condition.Code?.Text ?? condition.Code?.Coding.FirstOrDefault()?.Code ?? "(no-code)";
        var clinicalStatus = condition.ClinicalStatus?.Coding.FirstOrDefault()?.Code ?? condition.ClinicalStatus?.Text;

        return new ConditionSummaryViewModel(
            condition.Id ?? "(no-id)",
            patientId,
            codeText,
            clinicalStatus);
    }
}
