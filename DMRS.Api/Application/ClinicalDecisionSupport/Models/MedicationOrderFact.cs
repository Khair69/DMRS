namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class MedicationOrderFact
    {
        public MedicationOrderFact(string patientReference, IReadOnlyCollection<string> normalizedIngredientIds, string? medicationDisplay)
        {
            PatientReference = patientReference;
            NormalizedIngredientIds = normalizedIngredientIds;
            MedicationDisplay = medicationDisplay;
        }

        public string PatientReference { get; }
        public IReadOnlyCollection<string> NormalizedIngredientIds { get; }
        public string? MedicationDisplay { get; }
    }
}
