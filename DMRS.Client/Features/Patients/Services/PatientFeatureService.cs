using DMRS.Client.Features.Patients.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Patients.Services;

public class PatientFeatureService
{
    private readonly FhirApiService _fhirApiService;

    public PatientFeatureService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    public async Task<IReadOnlyList<PatientSummaryViewModel>> SearchPatientsAsync(string searchParam, string value)
    {
        var results = await _fhirApiService.SearchAsync<Patient>(searchParam, value);
        return results.Select(ToSummary).ToList();
    }

    public async Task<Patient?> GetPatientAsync(string id)
    {
        return await _fhirApiService.GetResourceAsync<Patient>(id);
    }

    public async Task<IReadOnlyList<Patient>> GetPatientHistoryAsync(string id)
    {
        return await _fhirApiService.GetHistoryAsync<Patient>(id);
    }

    public async Task<Patient?> CreatePatientAsync(PatientEditModel model)
    {
        var patient = model.ToFhirPatient();
        patient.Id = null;
        return await _fhirApiService.CreateResourceAsync<Patient>(patient);
    }

    public async Task<Patient?> UpdatePatientAsync(string id, PatientEditModel model)
    {
        var patient = model.ToFhirPatient();
        patient.Id = id;
        return await _fhirApiService.UpdateResourceAsync(id, patient);
    }

    public async System.Threading.Tasks.Task DeletePatientAsync(string id)
    {
        await _fhirApiService.DeleteResourceAsync<Patient>(id);
    }

    public static PatientSummaryViewModel ToSummary(Patient patient)
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
