using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class CdsVariableCatalog : ICdsVariableCatalog
    {
        private static readonly IReadOnlyList<CdsVariableDefinition> Variables =
        [
            new("hook", "string", "CDS hook identifier."),
            new("hookInstance", "string", "CDS hook instance identifier."),
            new("patientId", "string", "FHIR patient id from context."),
            new("medication.rxCui", "string", "Medication RxCUI or normalized code."),
            new("medication.name", "string", "Medication display name."),
            new("medication.ingredients", "array<string>", "Medication ingredient codes."),
            new("medication.indications", "array<string>", "Medication indication codes."),
            new("dose.requestedSingleMg", "number", "Requested single dose in mg when derivable."),
            new("dose.requestedDailyMg", "number", "Requested daily dose in mg when derivable."),
            new("dose.maxDailyMg", "number", "Knowledge-source max daily dose in mg."),
            new("dose.maxSingleMg", "number", "Knowledge-source max single dose in mg."),
            new("dose.warningThresholdMg", "number", "Knowledge-source warning threshold in mg."),
            new("safety.pregnancyCategory", "string", "Medicine pregnancy category."),
            new("safety.isControlled", "boolean", "Whether the medicine is controlled."),
            new("safety.allergyConflict", "boolean", "Whether patient allergies match medicine ingredients."),
            new("allergies.codes", "array<string>", "Patient allergy codes collected from stored AllergyIntolerance resources."),
            new("allergies.matches", "array<string>", "Ingredient codes that matched patient allergies."),
            new("context", "object", "Raw hook context payload."),
            new("prefetch", "object", "Raw hook prefetch payload.")
        ];

        public IReadOnlyList<CdsVariableDefinition> ListVariables() => Variables;
    }
}
