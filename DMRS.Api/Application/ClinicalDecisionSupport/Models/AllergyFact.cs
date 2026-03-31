namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class AllergyFact
    {
        public AllergyFact(string patientReference, IReadOnlyCollection<string> allergyCodes, string? allergyDisplay)
        {
            PatientReference = patientReference;
            AllergyCodes = allergyCodes;
            AllergyDisplay = allergyDisplay;
        }

        public string PatientReference { get; }
        public IReadOnlyCollection<string> AllergyCodes { get; }
        public string? AllergyDisplay { get; }
    }
}
