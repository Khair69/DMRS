using DMRS.Client.Features.Cds.Models;
using DMRS.Client.Features.Dashboard.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace DMRS.Client.Features.Dashboard.Services;

public sealed class DashboardFeatureService
{
    private readonly FhirApiService _fhirApiService;

    public DashboardFeatureService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
    }

    public async Task<DashboardSnapshotModel> GetSnapshotAsync()
    {
        var patientsTask = _fhirApiService.SearchResourcesAsync<Patient>();
        var appointmentsTask = _fhirApiService.SearchResourcesAsync<Appointment>();
        var medicationRequestsTask = _fhirApiService.SearchResourcesAsync<MedicationRequest>();
        var encountersTask = _fhirApiService.SearchResourcesAsync<Encounter>();
        var serviceRequestsTask = _fhirApiService.SearchResourcesAsync<ServiceRequest>();
        var conditionsTask = _fhirApiService.SearchResourcesAsync<Condition>();
        var rulesTask = _fhirApiService.GetApiJsonAsync<List<CdsRuleSummary>>("cds/rules");
        var alertsTask = _fhirApiService.GetApiJsonAsync<List<CdsAlertEventModel>>("cds/alerts");

        await Task.WhenAll(
            patientsTask,
            appointmentsTask,
            medicationRequestsTask,
            encountersTask,
            serviceRequestsTask,
            conditionsTask,
            rulesTask,
            alertsTask);

        var patients = patientsTask.Result;
        var appointments = appointmentsTask.Result;
        var medicationRequests = medicationRequestsTask.Result;
        var encounters = encountersTask.Result;
        var serviceRequests = serviceRequestsTask.Result;
        var conditions = conditionsTask.Result;
        var rules = rulesTask.Result ?? [];

        var today = DateTimeOffset.UtcNow;
        var upcomingAppointments = appointments
            .Where(a => TryGetDateTimeOffset(a.StartElement?.Value, out var start) && start >= today)
            .OrderBy(a => TryGetDateTimeOffset(a.StartElement?.Value, out var start) ? start : DateTimeOffset.MaxValue)
            .Take(5)
            .ToList();

        var recentMedicationRequests = medicationRequests
            .OrderByDescending(m => TryGetDateTimeOffset(m.AuthoredOnElement?.Value, out var authoredOn) ? authoredOn : DateTimeOffset.MinValue)
            .Take(5)
            .ToList();

        var assessablePatients = patients
            .Where(p => !string.IsNullOrWhiteSpace(p.Id)
                && !string.IsNullOrWhiteSpace(p.BirthDate)
                && (p.Gender == AdministrativeGender.Male || p.Gender == AdministrativeGender.Female))
            .ToList();

        // Assess every eligible patient, but cap how many risk requests are in flight
        // at once. Each request opens a DB connection server-side; firing all ~100 in
        // parallel exhausts PostgreSQL's max_connections ("too many clients already").
        const int maxConcurrentAssessments = 8;
        using var assessmentGate = new SemaphoreSlim(maxConcurrentAssessments);

        var riskTasks = assessablePatients
            .Select(async patient =>
            {
                await assessmentGate.WaitAsync();
                try
                {
                    var risk = await _fhirApiService.GetApiJsonAsync<HighUtilizationRiskAssessmentModel>(
                        $"cds/risk/high-utilization/{Uri.EscapeDataString(patient.Id!)}");
                    return (patient, risk);
                }
                finally
                {
                    assessmentGate.Release();
                }
            })
            .ToList();

        var allAssessed = (await Task.WhenAll(riskTasks))
            .Where(x => x.risk is not null)
            .ToList();

        var highRiskCount   = allAssessed.Count(x => x.risk!.RiskLevel == "High");
        var mediumRiskCount = allAssessed.Count(x => x.risk!.RiskLevel == "Medium");
        var lowRiskCount    = allAssessed.Count(x => x.risk!.RiskLevel == "Low");

        var watchlist = allAssessed
            .OrderByDescending(x => x.risk!.CompositeScore)
            .ThenByDescending(x => x.risk!.Probability ?? 0)
            .Take(5)
            .Select(x => new DashboardWatchlistItemModel(
                x.patient.Id ?? string.Empty,
                FormatPatientName(x.patient),
                x.risk!.FeaturesComplete
                    ? BuildWatchlistSummary(x.risk)
                    : "Missing age or gender for prediction",
                $"/patients/{x.patient.Id}",
                x.risk.IsHighRisk,
                x.risk.Probability,
                x.risk.RiskLevel,
                x.risk.CompositeScore,
                x.risk.ConditionCount,
                x.risk.MedicationCount,
                x.risk.RecentEncounterCount,
                x.risk.HasChronicConditions,
                x.risk.TopRiskFactors))
            .ToList();

        return new DashboardSnapshotModel
        {
            Metrics =
            [
                new("Patients", patients.Count.ToString(), "Registered people in the workspace", "metric-ocean", "/patients"),
                new("Appointments", upcomingAppointments.Count.ToString(), "Upcoming scheduled visits", "metric-sky", "/appointments"),
                new("Active Meds", medicationRequests.Count(m => m.Status == MedicationRequest.MedicationrequestStatus.Active).ToString(), "Medication requests on file", "metric-gold", "/medication-requests"),
                new("Encounters", encounters.Count.ToString(), "Documented clinical visits", "metric-emerald", "/encounters"),
                new("Service Requests", serviceRequests.Count.ToString(), "Orders and follow-up requests", "metric-ink", "/service-requests"),
                new("Conditions", conditions.Count.ToString(), "Tracked problem list items", "metric-rose", "/conditions")
            ],
            HighRiskPatients = watchlist,
            UpcomingAppointments = upcomingAppointments.Select(MapAppointment).ToList(),
            RecentMedicationRequests = recentMedicationRequests.Select(MapMedicationRequest).ToList(),
            ActiveRuleCount = rules.Count(r => r.IsActive),
            DraftRuleCount = rules.Count(r => r.HasUnpublishedChanges),
            HighRiskCount = highRiskCount,
            MediumRiskCount = mediumRiskCount,
            LowRiskCount = lowRiskCount,
            RecentAlerts = alertsTask.Result ?? []
        };
    }

    private static DashboardActivityItemModel MapAppointment(Appointment appointment)
    {
        var patientRef = appointment.Participant
            .FirstOrDefault(p => p.Actor?.Reference?.StartsWith("patient/", StringComparison.OrdinalIgnoreCase) == true)
            ?.Actor?.Reference ?? "Patient/unknown";

        var patientId = FhirReferenceHelper.ExtractReferenceId(patientRef, "patient") ?? "unknown";
        DateTimeOffset? start = TryGetDateTimeOffset(appointment.StartElement?.Value, out var appointmentStart)
            ? appointmentStart
            : null;

        return new DashboardActivityItemModel(
            appointment.Description ?? appointment.AppointmentType?.Text ?? "Scheduled appointment",
            $"Patient {patientId}",
            start?.ToLocalTime().ToString("dd MMM yyyy HH:mm") ?? appointment.SafeStatus(),
            $"/appointments/{appointment.Id}");
    }

    private static DashboardActivityItemModel MapMedicationRequest(MedicationRequest request)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(request.Subject?.Reference, "patient") ?? "unknown";
        var medication = request.Medication?.Concept?.Text ?? request.Medication?.Reference?.Reference ?? "Medication request";

        return new DashboardActivityItemModel(
            medication,
            $"Patient {patientId}",
            $"{request.SafeStatus()} | {request.SafeIntent()}",
            $"/medication-requests/{request.Id}");
    }

    private static string BuildWatchlistSummary(HighUtilizationRiskAssessmentModel risk)
    {
        var parts = new List<string>();
        if (risk.ConditionCount > 0) parts.Add($"{risk.ConditionCount} condition{(risk.ConditionCount == 1 ? "" : "s")}");
        if (risk.MedicationCount > 0) parts.Add($"{risk.MedicationCount} med{(risk.MedicationCount == 1 ? "" : "s")}");
        if (risk.RecentEncounterCount > 0) parts.Add($"{risk.RecentEncounterCount} recent visit{(risk.RecentEncounterCount == 1 ? "" : "s")}");
        var detail = parts.Count > 0 ? string.Join(" · ", parts) : $"Age {risk.Age:0}";
        return $"{risk.RiskLevel} risk · {detail}";
    }

    private static string FormatPatientName(Patient patient)
    {
        var name = patient.Name.FirstOrDefault();
        var parts = new[] { name?.Given?.FirstOrDefault(), name?.Family }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? $"Patient {patient.Id}" : string.Join(" ", parts);
    }

    private static bool TryGetDateTimeOffset(object? value, out DateTimeOffset parsed)
    {
        if (value is DateTimeOffset dto)
        {
            parsed = dto;
            return true;
        }

        if (DateTimeOffset.TryParse(value?.ToString(), out parsed))
        {
            return true;
        }

        parsed = default;
        return false;
    }
}
