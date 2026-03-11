using DMRS.Client.Features.Observations.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Observations.Services;

public sealed class ObservationFeatureService : FhirFeatureServiceBase<Observation, ObservationEditModel, ObservationSummaryViewModel>
{
    public ObservationFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override Observation ToResource(ObservationEditModel model)
        => model.ToFhirObservation();

    protected override ObservationSummaryViewModel MapToSummary(Observation observation)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(observation.Subject?.Reference, "patient") ?? "(unknown)";
        var codeText = observation.Code?.Text ?? observation.Code?.Coding.FirstOrDefault()?.Code ?? "(no-code)";
        var status = observation.Status?.ToString() ?? "unknown";

        return new ObservationSummaryViewModel(
            observation.Id ?? "(no-id)",
            patientId,
            codeText,
            status);
    }
}
