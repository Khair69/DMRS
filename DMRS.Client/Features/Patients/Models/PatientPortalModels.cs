using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Patients.Models;

/// <summary>
/// Everything the patient-facing home dashboard needs in one shot. All collections are already
/// scoped to the signed-in patient by the API (a patient resolves only to their own record).
/// </summary>
public sealed class PatientPortalDashboardModel
{
    public Patient? Patient { get; set; }

    public int ActiveMedicationCount { get; set; }
    public int ConditionCount { get; set; }
    public int AllergyCount { get; set; }
    public int UpcomingAppointmentCount { get; set; }

    public IReadOnlyList<Appointment> UpcomingAppointments { get; set; } = [];
    public IReadOnlyList<MedicationRequest> ActiveMedications { get; set; } = [];
    public IReadOnlyList<Condition> Conditions { get; set; } = [];
    public IReadOnlyList<Observation> RecentObservations { get; set; } = [];

    /// <summary>
    /// Gentle, patient-friendly wellness highlights derived from the readmission-risk model — never
    /// raw percentages or alarming "high risk" labels.
    /// </summary>
    public PatientWellnessModel Wellness { get; set; } = new();
}

public sealed class PatientWellnessModel
{
    public bool HasSignal { get; set; }
    public string Headline { get; set; } = "You're all set";
    public string Message { get; set; } = "We don't see anything that needs your attention right now.";
    public string Tone { get; set; } = "positive"; // positive | watch | attention
    public List<string> Tips { get; set; } = [];
}

/// <summary>
/// The full record set behind the "My Health" tabs. Unlike the dashboard snapshot, these lists are
/// not truncated so the patient can see everything in their file.
/// </summary>
public sealed class PatientPortalRecordsModel
{
    public Patient? Patient { get; set; }
    public IReadOnlyList<Condition> Conditions { get; set; } = [];
    public IReadOnlyList<MedicationRequest> Medications { get; set; } = [];
    public IReadOnlyList<AllergyIntolerance> Allergies { get; set; } = [];
    public IReadOnlyList<Observation> Observations { get; set; } = [];
    public IReadOnlyList<Encounter> Encounters { get; set; } = [];
    public IReadOnlyList<Appointment> Appointments { get; set; } = [];
    public IReadOnlyList<Procedure> Procedures { get; set; } = [];
    public IReadOnlyList<ServiceRequest> ServiceRequests { get; set; } = [];
}

/// <summary>Editable subset of the patient's own demographics. Mirrors the API request DTO.</summary>
public sealed class PatientProfileEditModel
{
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? BirthDate { get; set; }
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
}
