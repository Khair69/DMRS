using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using Hl7.Fhir.Model;
using NRules.Fluent.Dsl;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Rules
{
    public class MedicationAllergyRule : Rule
    {
        public override void Define()
        {
            MedicationOrderFact order = null!;
            AllergyFact allergy = null!;
            CdsAlertCollector collector = null!;

            When()
                .Match<MedicationOrderFact>(() => order, m => m.MedicationCodes.Count > 0)
                .Match<AllergyFact>(() => allergy,
                    a => a.PatientReference == order.PatientReference,
                    a => a.AllergyCodes.Count > 0,
                    a => a.AllergyCodes.Intersect(order.MedicationCodes).Any())
                .Match<CdsAlertCollector>(() => collector);

            Then()
                .Do(_ => collector.Add(new CdsAlert(
                    "medication-allergy-conflict",
                    $"Medication '{order.MedicationDisplay ?? "unknown"}' conflicts with recorded allergy '{allergy.AllergyDisplay ?? "unknown"}'.",
                    OperationOutcome.IssueSeverity.Error)));
        }
    }
}
