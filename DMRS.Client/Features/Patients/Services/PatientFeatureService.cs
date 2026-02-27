using DMRS.Client.Features.Patients.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Patients.Services;

public class PatientFeatureService : FhirFeatureServiceBase<Patient, PatientEditModel, PatientSummaryViewModel>
{
    public PatientFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override Patient ToResource(PatientEditModel model)
        => model.ToFhirPatient();

    protected override PatientSummaryViewModel MapToSummary(Patient patient)
    {
        var name = patient.Name.FirstOrDefault();
        var displayName = string.Join(" ", new[] { name?.Given?.FirstOrDefault(), name?.Family }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return new PatientSummaryViewModel(
            patient.Id ?? "(no-id)",
            string.IsNullOrWhiteSpace(displayName) ? "Unnamed patient" : displayName,
            patient.Gender.ToString(),
            patient.BirthDate,
            patient.Identifier.FirstOrDefault()?.Value);
    }
}
