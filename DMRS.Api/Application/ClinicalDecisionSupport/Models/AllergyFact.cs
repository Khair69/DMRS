namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class AllergyFact
    {
        public AllergyFact(string patientReference, IReadOnlyCollection<string> normalizedIngredientIds, string? allergyDisplay)
        {
            PatientReference = patientReference;
            NormalizedIngredientIds = normalizedIngredientIds;
            AllergyDisplay = allergyDisplay;
        }

        public string PatientReference { get; }
        public IReadOnlyCollection<string> NormalizedIngredientIds { get; }
        public string? AllergyDisplay { get; }
    }
}
