using DMRS.Client.Features.Dashboard.Models;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Patients.Models;

public sealed class PatientChartSnapshotModel
{
    public Patient? Patient { get; set; }
    public HighUtilizationRiskAssessmentModel? Risk { get; set; }
    public DiabetesRiskAssessmentModel? DiabetesRisk { get; set; }
    public CardiovascularRiskAssessmentModel? CardiovascularRisk { get; set; }
    public IReadOnlyList<AllergyIntolerance> Allergies { get; set; } = [];
    public IReadOnlyList<Condition> Conditions { get; set; } = [];
    public IReadOnlyList<Observation> Observations { get; set; } = [];
    public IReadOnlyList<MedicationRequest> MedicationRequests { get; set; } = [];
    public IReadOnlyList<Encounter> Encounters { get; set; } = [];
    public IReadOnlyList<Appointment> Appointments { get; set; } = [];
    public IReadOnlyList<ServiceRequest> ServiceRequests { get; set; } = [];
}
