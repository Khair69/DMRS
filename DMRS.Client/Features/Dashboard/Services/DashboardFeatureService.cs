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
        // Metric-tile totals come from a single cheap COUNT endpoint instead of fetching
        // entire collections (Encounters/Conditions/etc.) just to call .Count on them.
        var patientsTask = _fhirApiService.SearchResourcesAsync<Patient>();
        var appointmentsTask = _fhirApiService.SearchResourcesAsync<Appointment>();
        var medicationRequestsTask = _fhirApiService.SearchResourcesAsync<MedicationRequest>();
        var summaryTask = _fhirApiService.GetApiJsonAsync<DashboardSummaryModel>("analytics/dashboard-summary");
        var rulesTask = _fhirApiService.GetApiJsonAsync<List<CdsRuleSummary>>("cds/rules");
        var alertsTask = _fhirApiService.GetApiJsonAsync<List<CdsAlertEventModel>>("cds/alerts");

        await Task.WhenAll(
            patientsTask,
            appointmentsTask,
            medicationRequestsTask,
            summaryTask,
            rulesTask,
            alertsTask);

        var patients = patientsTask.Result;
        var appointments = appointmentsTask.Result;
        var medicationRequests = medicationRequestsTask.Result;
        var summary = summaryTask.Result ?? new DashboardSummaryModel();
        var rules = rulesTask.Result ?? [];

        var today = DateTimeOffset.UtcNow;
        var upcoming = appointments
            .Where(a => TryGetDateTimeOffset(a.StartElement?.Value, out var start) && start >= today)
            .OrderBy(a => TryGetDateTimeOffset(a.StartElement?.Value, out var start) ? start : DateTimeOffset.MaxValue)
            .ToList();
        // Tile shows the true number of upcoming visits; the preview panel below only lists the next 5.
        var upcomingCount = upcoming.Count;
        var upcomingAppointments = upcoming.Take(5).ToList();

        var recentMedicationRequests = medicationRequests
            .OrderByDescending(m => TryGetDateTimeOffset(m.AuthoredOnElement?.Value, out var authoredOn) ? authoredOn : DateTimeOffset.MinValue)
            .Take(5)
            .ToList();

        // Score the whole cohort in ONE request. The server loads everyone's data once and runs
        // the model per patient; doing it as 100 separate calls is throttled by the browser's
        // per-host connection cap, so it was the dashboard's biggest cost.
        var assessments = await _fhirApiService.GetApiJsonAsync<List<HighUtilizationRiskAssessmentModel>>(
            "cds/risk/high-utilization/batch") ?? [];

        // Patient display names come from the patients we already fetched, joined by id.
        var patientNameById = patients
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .ToDictionary(p => p.Id!, FormatPatientName);

        var highRiskCount   = assessments.Count(a => a.RiskLevel == "High");
        var mediumRiskCount = assessments.Count(a => a.RiskLevel == "Medium");
        var lowRiskCount    = assessments.Count(a => a.RiskLevel == "Low");

        var watchlist = assessments
            .OrderByDescending(a => a.CompositeScore)
            .ThenByDescending(a => a.Probability ?? 0)
            .Take(5)
            .Select(a => new DashboardWatchlistItemModel(
                a.PatientId,
                patientNameById.GetValueOrDefault(a.PatientId, $"Patient {a.PatientId}"),
                // Summary is composed at render time (DashboardText.WatchlistSummary) so it follows
                // live language switches; the structured counts below drive it.
                string.Empty,
                $"/patients/{a.PatientId}",
                a.IsHighRisk,
                a.Probability,
                a.RiskLevel,
                a.CompositeScore,
                a.ConditionCount,
                a.MedicationCount,
                a.RecentEncounterCount,
                a.HasChronicConditions,
                a.TopRiskFactors,
                a.Age,
                a.FeaturesComplete))
            .ToList();

        return new DashboardSnapshotModel
        {
            Metrics =
            [
                new("Patients", summary.Patients.ToString(), "Registered people in the workspace", "metric-ocean", "/patients", "nav.patients", "dashboard.metric.patientsRegisteredHelper"),
                new("Appointments", upcomingCount.ToString(), "Upcoming scheduled visits", "metric-sky", "/appointments", "nav.appointments", "dashboard.orgAdmin.appointmentsHelper"),
                new("Active Meds", summary.ActiveMedications.ToString(), "Medication requests on file", "metric-gold", "/medication-requests", "dashboard.metric.activeMeds", "dashboard.metric.activeMedsHelper"),
                new("Encounters", summary.Encounters.ToString(), "Documented clinical visits", "metric-emerald", "/encounters", "nav.encounters", "dashboard.metric.encountersHelper"),
                new("Service Requests", summary.ServiceRequests.ToString(), "Orders and follow-up requests", "metric-ink", "/service-requests", "nav.serviceRequests", "dashboard.metric.serviceRequestsHelper"),
                new("Conditions", summary.Conditions.ToString(), "Tracked problem list items", "metric-rose", "/conditions", "nav.conditions", "dashboard.metric.conditionsHelper")
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

    /// <summary>
    /// Lean snapshot for the doctor dashboard. Hits only the scoped aggregate endpoints (counts,
    /// risk batch, alerts) — no full Patient/Appointment/MedicationRequest collection fetches — and
    /// passes <paramref name="mine"/> so the server narrows to the doctor's panel ("my patients") or
    /// their whole organization. The watchlist names come from the risk batch itself (DisplayName).
    /// </summary>
    public async Task<DoctorSnapshotModel> GetDoctorSnapshotAsync(bool mine)
    {
        var query = mine ? "?mine=true" : string.Empty;

        var summaryTask = _fhirApiService.GetApiJsonAsync<DashboardSummaryModel>($"analytics/dashboard-summary{query}");
        var assessmentsTask = _fhirApiService.GetApiJsonAsync<List<HighUtilizationRiskAssessmentModel>>(
            $"cds/risk/high-utilization/batch{query}");
        var alertsTask = _fhirApiService.GetApiJsonAsync<List<CdsAlertEventModel>>($"cds/alerts{query}");

        await Task.WhenAll(summaryTask, assessmentsTask, alertsTask);

        var summary = summaryTask.Result ?? new DashboardSummaryModel();
        var assessments = assessmentsTask.Result ?? [];

        var watchlist = assessments
            .OrderByDescending(a => a.CompositeScore)
            .ThenByDescending(a => a.Probability ?? 0)
            .Take(5)
            .Select(a => new DashboardWatchlistItemModel(
                a.PatientId,
                string.IsNullOrWhiteSpace(a.DisplayName) ? $"Patient {a.PatientId}" : a.DisplayName,
                // Composed at render time — see DashboardText.WatchlistSummary.
                string.Empty,
                $"/patients/{a.PatientId}",
                a.IsHighRisk,
                a.Probability,
                a.RiskLevel,
                a.CompositeScore,
                a.ConditionCount,
                a.MedicationCount,
                a.RecentEncounterCount,
                a.HasChronicConditions,
                a.TopRiskFactors,
                a.Age,
                a.FeaturesComplete))
            .ToList();

        return new DoctorSnapshotModel
        {
            PatientCount = summary.Patients,
            ActiveMedications = summary.ActiveMedications,
            Conditions = summary.Conditions,
            ServiceRequests = summary.ServiceRequests,
            Encounters = summary.Encounters,
            HighRiskCount = assessments.Count(a => a.RiskLevel == "High"),
            MediumRiskCount = assessments.Count(a => a.RiskLevel == "Medium"),
            LowRiskCount = assessments.Count(a => a.RiskLevel == "Low"),
            Watchlist = watchlist,
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
