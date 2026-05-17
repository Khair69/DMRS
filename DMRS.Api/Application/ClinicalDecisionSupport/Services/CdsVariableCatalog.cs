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
            new("patient.id", "string", "Normalized patient id when resolvable."),
            new("patient.gender", "string", "Patient administrative gender."),
            new("patient.birthDate", "string", "Patient birth date from stored Patient resource."),
            new("patient.ageYears", "number", "Patient age in years when birth date is available."),
            new("medication.rxCui", "string", "Medication RxCUI or normalized code."),
            new("medication.name", "string", "Medication display name."),
            new("medication.ingredients", "array<string>", "Medication ingredient codes."),
            new("medication.ingredientNames", "array<string>", "Medication ingredient names."),
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
            new("conditions.codes", "array<string>", "Active patient condition codes collected from stored Condition resources."),
            new("conditions.texts", "array<string>", "Active patient condition display texts."),
            new("therapy.activeMedicationRxCuis", "array<string>", "Active patient medication RxCUIs excluding the in-flight medication when identifiable."),
            new("therapy.activeMedicationNames", "array<string>", "Active patient medication names excluding the in-flight medication when identifiable."),
            new("therapy.activeIngredientCodes", "array<string>", "Ingredient codes across other active patient medications."),
            new("therapy.duplicateIngredientMatches", "array<string>", "Ingredient codes shared by the in-flight medication and other active medications."),
            new("therapy.duplicateIngredientConflict", "boolean", "Whether the in-flight medication shares ingredients with other active medications."),
            new("context", "object", "Raw hook context payload."),
            new("prefetch", "object", "Raw hook prefetch payload.")
        ];

        public IReadOnlyList<CdsVariableDefinition> ListVariables() => Variables;
    }
}
