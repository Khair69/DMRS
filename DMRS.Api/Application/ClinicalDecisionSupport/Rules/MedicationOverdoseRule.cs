using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using Hl7.Fhir.Model;
using NRules.Fluent.Dsl;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Rules
{
    public class MedicationOverdoseRule : Rule
    {
        public override void Define()
        {
            MedicationOrderFact order = null!;
            DoseFact dose = null!;
            DrugKnowledge knowledge = null!;
            CdsAlertCollector collector = null!;

            When()
                .Match<MedicationOrderFact>(() => order, m => m.NormalizedIngredientIds.Count > 0)
                .Match<DoseFact>(() => dose, d => d.DailyDoseMg > 0)
                .Match<DrugKnowledge>(() => knowledge,
                    k => order.NormalizedIngredientIds.Contains(k.Code),
                    k => dose.DailyDoseMg > k.MaxDailyDoseMg)
                .Match<CdsAlertCollector>(() => collector);

            Then()
                .Do(_ => collector.Add(new CdsAlert(
                    "medication-overdose",
                    $"Requested daily dose ({dose.DailyDoseMg:0.##} mg) exceeds max daily dose ({knowledge.MaxDailyDoseMg:0.##} mg) for '{order.MedicationDisplay ?? knowledge.Display ?? knowledge.Code}'.",
                    OperationOutcome.IssueSeverity.Error)));
        }
    }
}
