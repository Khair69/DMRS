using DMRS.Client.Features.Patients.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;
using DMRS.Client.Features.Dashboard.Models;
using Task = System.Threading.Tasks.Task;

namespace DMRS.Client.Features.Patients.Services;

public sealed class PatientChartFeatureService
{
    private readonly FhirApiService _fhirApiService;

    public PatientChartFeatureService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    public async Task<PatientChartSnapshotModel?> GetSnapshotAsync(string patientId)
    {
        var patient = await _fhirApiService.GetResourceAsync<Patient>(patientId);
        if (patient is null)
        {
            return null;
        }

        var patientReference = $"patient/{patientId}".ToLowerInvariant();
        var query = new Dictionary<string, string> { ["patient"] = patientReference };

        var allergiesTask = _fhirApiService.SearchResourcesAsync<AllergyIntolerance>(query);
        var conditionsTask = _fhirApiService.SearchResourcesAsync<Condition>(query);
        var observationsTask = _fhirApiService.SearchResourcesAsync<Observation>(query);
        var medicationRequestsTask = _fhirApiService.SearchResourcesAsync<MedicationRequest>(query);
        var encountersTask = _fhirApiService.SearchResourcesAsync<Encounter>(query);
        var appointmentsTask = _fhirApiService.SearchResourcesAsync<Appointment>(query);
        var serviceRequestsTask = _fhirApiService.SearchResourcesAsync<ServiceRequest>(query);
        var riskTask = _fhirApiService.GetApiJsonAsync<HighUtilizationRiskAssessmentModel>($"cds/risk/high-utilization/{patientId}");
        var diabetesRiskTask = SafeGetAsync<DiabetesRiskAssessmentModel>($"cds/risk/diabetes/{patientId}");
        var cardiovascularRiskTask = SafeGetAsync<CardiovascularRiskAssessmentModel>($"cds/risk/cardiovascular/{patientId}");

        await Task.WhenAll(
            allergiesTask,
            conditionsTask,
            observationsTask,
            medicationRequestsTask,
            encountersTask,
            appointmentsTask,
            serviceRequestsTask,
            riskTask,
            diabetesRiskTask,
            cardiovascularRiskTask);

        return new PatientChartSnapshotModel
        {
            Patient = patient,
            Risk = riskTask.Result,
            DiabetesRisk = diabetesRiskTask.Result,
            CardiovascularRisk = cardiovascularRiskTask.Result,
            Allergies = allergiesTask.Result.Take(6).ToList(),
            Conditions = conditionsTask.Result.Take(6).ToList(),
            Observations = observationsTask.Result.Take(6).ToList(),
            MedicationRequests = medicationRequestsTask.Result.Take(6).ToList(),
            Encounters = encountersTask.Result.Take(6).ToList(),
            Appointments = appointmentsTask.Result.Take(6).ToList(),
            ServiceRequests = serviceRequestsTask.Result.Take(6).ToList()
        };
    }

    // The newer AI models may not be trained/deployed yet; never let a failed risk call break the
    // whole patient chart load.
    private async Task<T?> SafeGetAsync<T>(string path)
    {
        try
        {
            return await _fhirApiService.GetApiJsonAsync<T>(path);
        }
        catch
        {
            return default;
        }
    }
}
