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
        var upcomingAppointments = appointments
            .Where(a => TryGetDateTimeOffset(a.StartElement?.Value, out var start) && start >= today)
            .OrderBy(a => TryGetDateTimeOffset(a.StartElement?.Value, out var start) ? start : DateTimeOffset.MaxValue)
            .Take(5)
            .ToList();

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
                a.FeaturesComplete
                    ? BuildWatchlistSummary(a)
                    : "Missing age or gender for prediction",
                $"/patients/{a.PatientId}",
                a.IsHighRisk,
                a.Probability,
                a.RiskLevel,
                a.CompositeScore,
                a.ConditionCount,
                a.MedicationCount,
                a.RecentEncounterCount,
                a.HasChronicConditions,
                a.TopRiskFactors))
            .ToList();

        return new DashboardSnapshotModel
        {
            Metrics =
            [
                new("Patients", summary.Patients.ToString(), "Registered people in the workspace", "metric-ocean", "/patients"),
                new("Appointments", upcomingAppointments.Count.ToString(), "Upcoming scheduled visits", "metric-sky", "/appointments"),
                new("Active Meds", summary.ActiveMedications.ToString(), "Medication requests on file", "metric-gold", "/medication-requests"),
                new("Encounters", summary.Encounters.ToString(), "Documented clinical visits", "metric-emerald", "/encounters"),
                new("Service Requests", summary.ServiceRequests.ToString(), "Orders and follow-up requests", "metric-ink", "/service-requests"),
                new("Conditions", summary.Conditions.ToString(), "Tracked problem list items", "metric-rose", "/conditions")
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
