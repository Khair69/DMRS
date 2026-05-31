using DMRS.Client.Features.Encounters.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Encounters.Services;

public sealed class EncounterFeatureService : FhirFeatureServiceBase<Encounter, EncounterEditModel, EncounterSummaryViewModel>
{
    public EncounterFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override Encounter ToResource(EncounterEditModel model)
        => model.ToFhirEncounter();

    protected override EncounterSummaryViewModel MapToSummary(Encounter encounter)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(encounter.Subject?.Reference, "patient") ?? "(unknown)";
        string classCode;
        try { classCode = encounter.Class?.FirstOrDefault()?.Coding.FirstOrDefault()?.Code ?? "(no-class)"; }
        catch { classCode = "(no-class)"; }

        return new EncounterSummaryViewModel(
            encounter.Id ?? "(no-id)",
            patientId,
            encounter.SafeStatus(),
            classCode);
    }
}
