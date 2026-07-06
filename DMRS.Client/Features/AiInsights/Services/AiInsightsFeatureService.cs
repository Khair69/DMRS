using DMRS.Client.Features.AiInsights.Models;
using DMRS.Client.Features.Dashboard.Models;
using DMRS.Client.Features.Patients.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace DMRS.Client.Features.AiInsights.Services;

/// <summary>
/// Aggregates every AI model into a single page payload. Each model is scored across the whole
/// cohort in one batch request (the per-model "/batch" endpoints), patient names are joined in from
/// the patient list, and the rows are ranked into a watchlist with distribution counts.
/// </summary>
public sealed class AiInsightsFeatureService
{
    private readonly FhirApiService _fhirApiService;
    private readonly LocalizationService _loc;
    private const int WatchlistSize = 8;

    public AiInsightsFeatureService(FhirApiService fhirApiService, LocalizationService loc)
    {
        _fhirApiService = fhirApiService;
        _loc = loc;
    }

    public async Task<AiInsightsSnapshot> GetSnapshotAsync()
    {
        var patientsTask = _fhirApiService.SearchResourcesAsync<Patient>();
        var readmissionTask = _fhirApiService.GetApiJsonAsync<List<HighUtilizationRiskAssessmentModel>>("cds/risk/high-utilization/batch");
        var diabetesTask = _fhirApiService.GetApiJsonAsync<List<DiabetesRiskAssessmentModel>>("cds/risk/diabetes/batch");
        var cardiovascularTask = _fhirApiService.GetApiJsonAsync<List<CardiovascularRiskAssessmentModel>>("cds/risk/cardiovascular/batch");
        var prevalenceTask = _fhirApiService.GetApiJsonAsync<List<ConditionPrevalenceItem>>("analytics/condition-prevalence");

        await Task.WhenAll(patientsTask, readmissionTask, diabetesTask, cardiovascularTask, prevalenceTask);

        var nameById = patientsTask.Result
            .Where(p => !string.IsNullOrWhiteSpace(p.Id))
            .ToDictionary(p => p.Id!, FormatPatientName);

        var readmission = readmissionTask.Result ?? [];
        var diabetes = diabetesTask.Result ?? [];
        var cardiovascular = cardiovascularTask.Result ?? [];

        var models = new List<AiModelCohort>
        {
            BuildCohort(
                new AiModelCohort
                {
                    Key = "readmission",
                    Title = _loc["ai.model.readmission.title"],
                    Predicts = _loc["ai.model.readmission.predicts"],
                    Dataset = "UCI \"130-US hospitals\"",
                    Accuracy = "0.65",
                    Auc = "0.63",
                    Features =
                    [
                        _loc["ai.feature.age"], _loc["ai.feature.gender"], _loc["ai.feature.conditions"],
                        _loc["ai.feature.activeMeds"], _loc["ai.feature.recentVisits"], _loc["ai.feature.procedures"],
                    ],
                    AccentClass = "metric-rose",
                },
                readmission.Select(a => new AiPatientRiskRow(
                    a.PatientId,
                    nameById.GetValueOrDefault(a.PatientId, $"Patient {a.PatientId}"),
                    $"/patients/{a.PatientId}",
                    a.RiskLevel,
                    a.CompositeScore,
                    a.FeaturesComplete,
                    ReadmissionDetail(a)))),

            BuildCohort(
                new AiModelCohort
                {
                    Key = "diabetes",
                    Title = _loc["ai.model.diabetes.title"],
                    Predicts = _loc["ai.model.diabetes.predicts"],
                    Dataset = "Pima Indians Diabetes",
                    Accuracy = "0.73",
                    Auc = "0.82",
                    Features =
                    [
                        _loc["ai.feature.glucose"], _loc["ai.feature.diastolicBp"], _loc["ai.feature.bmi"], _loc["ai.feature.age"],
                    ],
                    AccentClass = "metric-gold",
                },
                diabetes.Select(a => new AiPatientRiskRow(
                    a.PatientId,
                    nameById.GetValueOrDefault(a.PatientId, $"Patient {a.PatientId}"),
                    $"/patients/{a.PatientId}",
                    a.RiskLevel,
                    a.Probability ?? 0f,
                    a.FeaturesComplete,
                    _loc["ai.detail.diabetes", a.Glucose, a.Bmi]))),

            BuildCohort(
                new AiModelCohort
                {
                    Key = "cardiovascular",
                    Title = _loc["ai.model.cardiovascular.title"],
                    Predicts = _loc["ai.model.cardiovascular.predicts"],
                    Dataset = "UCI Heart Disease",
                    Accuracy = "0.81",
                    Auc = "0.93",
                    Features =
                    [
                        _loc["ai.feature.age"], _loc["ai.feature.sex"], _loc["ai.feature.restingBp"],
                        _loc["ai.feature.cholesterol"], _loc["ai.feature.maxHeartRate"], _loc["ai.feature.fastingBloodSugar"],
                    ],
                    AccentClass = "metric-ocean",
                },
                cardiovascular.Select(a => new AiPatientRiskRow(
                    a.PatientId,
                    nameById.GetValueOrDefault(a.PatientId, $"Patient {a.PatientId}"),
                    $"/patients/{a.PatientId}",
                    a.RiskLevel,
                    a.Probability ?? 0f,
                    a.FeaturesComplete,
                    _loc["ai.detail.cardio", a.RestingBloodPressure, a.Cholesterol]))),
        };

        return new AiInsightsSnapshot
        {
            Models = models,
            ConditionPrevalence = prevalenceTask.Result ?? [],
        };
    }

    private static AiModelCohort BuildCohort(AiModelCohort cohort, IEnumerable<AiPatientRiskRow> rows)
    {
        var all = rows.OrderByDescending(r => r.Score).ToList();
        cohort.HighCount = all.Count(r => r.RiskLevel == "High");
        cohort.MediumCount = all.Count(r => r.RiskLevel == "Medium");
        cohort.LowCount = all.Count(r => r.RiskLevel == "Low");
        cohort.ImputedCount = all.Count(r => !r.FeaturesComplete);
        cohort.AllRows = all;
        cohort.Watchlist = all.Take(WatchlistSize).ToList();
        return cohort;
    }

    private string ReadmissionDetail(HighUtilizationRiskAssessmentModel a)
    {
        var parts = new List<string>();
        if (a.ConditionCount > 0) parts.Add(_loc["ai.detail.conditions", a.ConditionCount]);
        if (a.MedicationCount > 0) parts.Add(_loc["ai.detail.meds", a.MedicationCount]);
        if (a.RecentEncounterCount > 0) parts.Add(_loc["ai.detail.visits", a.RecentEncounterCount]);
        return parts.Count > 0 ? string.Join(" · ", parts) : _loc["ai.detail.age", (int)a.Age];
    }

    private static string FormatPatientName(Patient patient)
    {
        var name = patient.Name.FirstOrDefault();
        var parts = new[] { name?.Given?.FirstOrDefault(), name?.Family }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? $"Patient {patient.Id}" : string.Join(" ", parts);
    }
}
