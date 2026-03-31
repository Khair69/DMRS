namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class MedicationOrderFact
    {
        public MedicationOrderFact(string patientReference, IReadOnlyCollection<string> medicationCodes, string? medicationDisplay)
        {
            PatientReference = patientReference;
            MedicationCodes = medicationCodes;
            MedicationDisplay = medicationDisplay;
        }

        public string PatientReference { get; }
        public IReadOnlyCollection<string> MedicationCodes { get; }
        public string? MedicationDisplay { get; }
    }
}
