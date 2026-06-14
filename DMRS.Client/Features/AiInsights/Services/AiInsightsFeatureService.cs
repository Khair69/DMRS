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
    private const int WatchlistSize = 8;

    public AiInsightsFeatureService(FhirApiService fhirApiService)
    {
        _fhirApiService = fhirApiService;
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
                    Title = "30-Day Readmission Risk",
                    Predicts = "Chance of an unplanned hospital readmission within 30 days",
                    Dataset = "UCI \"130-US hospitals\"",
                    Accuracy = "0.65",
                    Auc = "0.63",
                    Features = ["Age", "Gender", "# conditions", "# active meds", "# recent visits", "# procedures"],
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
                    Title = "Type-2 Diabetes Risk",
                    Predicts = "Likelihood of type-2 diabetes",
                    Dataset = "Pima Indians Diabetes",
                    Accuracy = "0.73",
                    Auc = "0.82",
                    Features = ["Glucose", "Diastolic BP", "BMI", "Age"],
                    AccentClass = "metric-gold",
                },
                diabetes.Select(a => new AiPatientRiskRow(
                    a.PatientId,
                    nameById.GetValueOrDefault(a.PatientId, $"Patient {a.PatientId}"),
                    $"/patients/{a.PatientId}",
                    a.RiskLevel,
                    a.Probability ?? 0f,
                    a.FeaturesComplete,
                    $"Glucose {a.Glucose:0} mg/dL · BMI {a.Bmi:0.0}"))),

            BuildCohort(
                new AiModelCohort
                {
                    Key = "cardiovascular",
                    Title = "Cardiovascular Risk",
                    Predicts = "Likelihood of coronary heart disease",
                    Dataset = "UCI Heart Disease",
                    Accuracy = "0.81",
                    Auc = "0.93",
                    Features = ["Age", "Sex", "Resting BP", "Cholesterol", "Max heart rate", "Fasting blood sugar"],
                    AccentClass = "metric-ocean",
                },
                cardiovascular.Select(a => new AiPatientRiskRow(
                    a.PatientId,
                    nameById.GetValueOrDefault(a.PatientId, $"Patient {a.PatientId}"),
                    $"/patients/{a.PatientId}",
                    a.RiskLevel,
                    a.Probability ?? 0f,
                    a.FeaturesComplete,
                    $"BP {a.RestingBloodPressure:0} · Chol {a.Cholesterol:0} mg/dL"))),
        };

        return new AiInsightsSnapshot
        {
            Models = models,
            ConditionPrevalence = prevalenceTask.Result ?? [],
        };
    }

    private static AiModelCohort BuildCohort(AiModelCohort cohort, IEnumerable<AiPatientRiskRow> rows)
    {
        var all = rows.ToList();
        cohort.HighCount = all.Count(r => r.RiskLevel == "High");
        cohort.MediumCount = all.Count(r => r.RiskLevel == "Medium");
        cohort.LowCount = all.Count(r => r.RiskLevel == "Low");
        cohort.ImputedCount = all.Count(r => !r.FeaturesComplete);
        cohort.Watchlist = all
            .OrderByDescending(r => r.Score)
            .Take(WatchlistSize)
            .ToList();
        return cohort;
    }

    private static string ReadmissionDetail(HighUtilizationRiskAssessmentModel a)
    {
        var parts = new List<string>();
        if (a.ConditionCount > 0) parts.Add($"{a.ConditionCount} condition{(a.ConditionCount == 1 ? "" : "s")}");
        if (a.MedicationCount > 0) parts.Add($"{a.MedicationCount} med{(a.MedicationCount == 1 ? "" : "s")}");
        if (a.RecentEncounterCount > 0) parts.Add($"{a.RecentEncounterCount} visit{(a.RecentEncounterCount == 1 ? "" : "s")}");
        return parts.Count > 0 ? string.Join(" · ", parts) : $"Age {a.Age:0}";
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
