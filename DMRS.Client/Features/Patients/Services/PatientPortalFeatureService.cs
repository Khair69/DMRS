using DMRS.Client.Features.Dashboard.Models;
using DMRS.Client.Features.Patients.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace DMRS.Client.Features.Patients.Services;

/// <summary>
/// Backs the patient self-service portal. Everything it reads is automatically scoped by the API to
/// the signed-in patient's own record, so no patient id needs to be passed from the UI.
/// </summary>
public sealed class PatientPortalFeatureService
{
    private const string MePatientPath = "api/me/patient";

    private readonly FhirApiService _fhirApiService;

    public PatientPortalFeatureService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    public Task<Patient?> GetMyPatientAsync() => _fhirApiService.GetFhirFromPathAsync<Patient>(MePatientPath);

    public async Task<PatientPortalDashboardModel?> GetDashboardAsync()
    {
        var patient = await GetMyPatientAsync();
        if (patient?.Id is null)
        {
            return null;
        }

        var query = PatientQuery(patient.Id);

        var medicationsTask = _fhirApiService.SearchResourcesAsync<MedicationRequest>(query);
        var conditionsTask = _fhirApiService.SearchResourcesAsync<Condition>(query);
        var allergiesTask = _fhirApiService.SearchResourcesAsync<AllergyIntolerance>(query);
        var appointmentsTask = _fhirApiService.SearchResourcesAsync<Appointment>(query);
        var observationsTask = _fhirApiService.SearchResourcesAsync<Observation>(query);
        var riskTask = SafeGetAsync<HighUtilizationRiskAssessmentModel>($"cds/risk/high-utilization/{patient.Id}");

        await Task.WhenAll(
            medicationsTask, conditionsTask, allergiesTask, appointmentsTask, observationsTask, riskTask);

        var medications = medicationsTask.Result;
        var appointments = appointmentsTask.Result;

        var activeMedications = medications
            .Where(m => IsActiveMedication(m.SafeStatus()))
            .ToList();

        var now = DateTimeOffset.Now;
        var upcoming = appointments
            .Select(a => (Appointment: a, Start: AppointmentStart(a)))
            .Where(x => x.Start is not null && x.Start >= now)
            .OrderBy(x => x.Start)
            .Select(x => x.Appointment)
            .ToList();

        var recentObservations = observationsTask.Result
            .OrderByDescending(ObservationWhen)
            .Take(6)
            .ToList();

        return new PatientPortalDashboardModel
        {
            Patient = patient,
            ActiveMedicationCount = activeMedications.Count,
            ConditionCount = conditionsTask.Result.Count,
            AllergyCount = allergiesTask.Result.Count,
            UpcomingAppointmentCount = upcoming.Count,
            UpcomingAppointments = upcoming.Take(5).ToList(),
            ActiveMedications = activeMedications.Take(5).ToList(),
            Conditions = conditionsTask.Result.Take(6).ToList(),
            RecentObservations = recentObservations,
            Wellness = BuildWellness(riskTask.Result, conditionsTask.Result.Count, activeMedications.Count)
        };
    }

    public async Task<PatientPortalRecordsModel?> GetRecordsAsync()
    {
        var patient = await GetMyPatientAsync();
        if (patient?.Id is null)
        {
            return null;
        }

        var query = PatientQuery(patient.Id);

        var conditionsTask = _fhirApiService.SearchResourcesAsync<Condition>(query);
        var medicationsTask = _fhirApiService.SearchResourcesAsync<MedicationRequest>(query);
        var allergiesTask = _fhirApiService.SearchResourcesAsync<AllergyIntolerance>(query);
        var observationsTask = _fhirApiService.SearchResourcesAsync<Observation>(query);
        var encountersTask = _fhirApiService.SearchResourcesAsync<Encounter>(query);
        var appointmentsTask = _fhirApiService.SearchResourcesAsync<Appointment>(query);
        var proceduresTask = _fhirApiService.SearchResourcesAsync<Procedure>(query);
        var serviceRequestsTask = _fhirApiService.SearchResourcesAsync<ServiceRequest>(query);

        await Task.WhenAll(
            conditionsTask, medicationsTask, allergiesTask, observationsTask,
            encountersTask, appointmentsTask, proceduresTask, serviceRequestsTask);

        return new PatientPortalRecordsModel
        {
            Patient = patient,
            Conditions = conditionsTask.Result.ToList(),
            Medications = medicationsTask.Result.ToList(),
            Allergies = allergiesTask.Result.ToList(),
            Observations = observationsTask.Result.OrderByDescending(ObservationWhen).ToList(),
            Encounters = encountersTask.Result.ToList(),
            Appointments = appointmentsTask.Result
                .OrderByDescending(a => AppointmentStart(a) ?? DateTimeOffset.MinValue)
                .ToList(),
            Procedures = proceduresTask.Result.ToList(),
            ServiceRequests = serviceRequestsTask.Result.ToList()
        };
    }

    public Task<Patient?> UpdateProfileAsync(PatientProfileEditModel model)
    {
        var payload = new
        {
            givenName = model.GivenName,
            familyName = model.FamilyName,
            phone = model.Phone,
            email = model.Email,
            birthDate = model.BirthDate,
            gender = model.Gender,
            maritalStatus = model.MaritalStatus,
            address = new
            {
                line = model.AddressLine,
                city = model.City,
                state = model.State,
                postalCode = model.PostalCode,
                country = model.Country
            }
        };

        return _fhirApiService.PutFhirToPathAsync<Patient, object>(MePatientPath, payload);
    }

    private static Dictionary<string, string> PatientQuery(string patientId)
        => new() { ["patient"] = $"Patient/{patientId}" };

    private static bool IsActiveMedication(string status)
        => string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "on-hold", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset? AppointmentStart(Appointment appointment)
        => DateTimeOffset.TryParse(appointment.StartElement?.Value?.ToString(), out var start) ? start : null;

    private static DateTimeOffset ObservationWhen(Observation observation)
    {
        if (observation.Effective is FhirDateTime dateTime
            && DateTimeOffset.TryParse(dateTime.Value, out var effective))
        {
            return effective;
        }

        if (DateTimeOffset.TryParse(observation.IssuedElement?.Value?.ToString(), out var issued))
        {
            return issued;
        }

        return DateTimeOffset.MinValue;
    }

    private static PatientWellnessModel BuildWellness(
        HighUtilizationRiskAssessmentModel? risk, int conditionCount, int activeMedicationCount)
    {
        var baseTips = new List<string>
        {
            "Keep your contact details up to date so your care team can reach you.",
            "Bring a list of your current medications to every appointment."
        };

        if (risk is null || !risk.FeaturesComplete)
        {
            return new PatientWellnessModel
            {
                HasSignal = false,
                Tone = "positive",
                Headline = "Your health summary",
                Message = "Here's a friendly overview of your care. Your clinical team keeps the full picture.",
                Tips = baseTips
            };
        }

        return risk.RiskLevel switch
        {
            "High" => new PatientWellnessModel
            {
                HasSignal = true,
                Tone = "attention",
                Headline = "Your care team is keeping a close eye on you",
                Message = "Staying connected with your clinicians right now makes a real difference. "
                    + "Don't skip upcoming visits, and reach out if something feels off.",
                Tips =
                [
                    "Attend your scheduled appointments and follow-ups.",
                    "Take your medications exactly as prescribed.",
                    "Contact your care team early if symptoms change."
                ]
            },
            "Medium" => new PatientWellnessModel
            {
                HasSignal = true,
                Tone = "watch",
                Headline = "A few things worth staying on top of",
                Message = "You're doing well — keeping up with your care plan will help you stay that way.",
                Tips =
                [
                    "Keep up with routine check-ups.",
                    .. baseTips
                ]
            },
            _ => new PatientWellnessModel
            {
                HasSignal = true,
                Tone = "positive",
                Headline = "You're on a good track",
                Message = conditionCount == 0 && activeMedicationCount == 0
                    ? "Nothing needs your attention right now. Keep up the healthy habits!"
                    : "Things look steady. Keep following your care plan and you're set.",
                Tips = baseTips
            }
        };
    }

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
