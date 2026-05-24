using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class RuleTemplateService : IRuleTemplateService
    {
        private const string MaxDoseTemplate = "max-dose-exceeded";
        private const string AllergyConflictTemplate = "allergy-conflict";
        private const string PregnancyWarningTemplate = "pregnancy-category-warning";
        private const string ControlledMedicationTemplate = "controlled-medication-warning";
        private const string IndicationMismatchTemplate = "indication-mismatch";
        private const string DuplicateIngredientConflictTemplate = "duplicate-ingredient-conflict";
        private const string HighUtilizationRiskTemplate = "high-utilization-risk-warning";
        private const string PolypharmacyWarningTemplate = "polypharmacy-warning";
        private const string HighRiskPatientViewTemplate = "high-risk-patient-view";

        private static readonly IReadOnlyList<RuleTemplateDefinition> Templates =
        [
            new(
                MaxDoseTemplate,
                "Max Dose Exceeded",
                "Warn when requested daily dose exceeds the stored maximum daily dose.",
                [
                    new RuleTemplateParameterDefinition("medicationRxCui", "string", false, "Optional RxCUI restriction for the rule."),
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name.")
                ]),
            new(
                AllergyConflictTemplate,
                "Allergy Conflict",
                "Warn when the patient's allergy codes match the medication ingredient codes.",
                [
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name.")
                ]),
            new(
                PregnancyWarningTemplate,
                "Pregnancy Category Warning",
                "Warn when the medication belongs to a specified pregnancy category.",
                [
                    new RuleTemplateParameterDefinition("pregnancyCategory", "string", true, "Pregnancy category to match, such as D or X."),
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name.")
                ]),
            new(
                ControlledMedicationTemplate,
                "Controlled Medication Warning",
                "Warn when the selected medication is flagged as controlled.",
                [
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name.")
                ]),
            new(
                IndicationMismatchTemplate,
                "Indication Mismatch",
                "Warn when a requested indication code is not listed in the medication indications.",
                [
                    new RuleTemplateParameterDefinition("indicationCode", "string", true, "Expected indication code, such as ICD-10."),
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name.")
                ]),
            new(
                DuplicateIngredientConflictTemplate,
                "Duplicate Ingredient Conflict",
                "Warn when the in-flight medication shares ingredients with other active patient medications.",
                [
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name.")
                ]),
            new(
                HighUtilizationRiskTemplate,
                "High Utilization Risk",
                "Warn when the AI model predicts that the patient is likely to become a frequent flyer / high-utilization patient.",
                [
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name."),
                    new RuleTemplateParameterDefinition("highUtilizationProbabilityThreshold", "number", false, "Optional minimum AI probability required to trigger the warning.")
                ]),
            new(
                PolypharmacyWarningTemplate,
                "Polypharmacy Warning",
                "Warn when a patient has 5 or more active medications, indicating a polypharmacy risk.",
                [
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name.")
                ]),
            new(
                HighRiskPatientViewTemplate,
                "High-Risk Patient Alert (patient-view)",
                "Display a risk alert when opening a patient chart with a high composite risk score.",
                [
                    new RuleTemplateParameterDefinition("name", "string", true, "Human-readable rule name.")
                ])
        ];

        public IReadOnlyList<RuleTemplateDefinition> ListTemplates() => Templates;

        public CdsRuleDefinition Compile(RuleTemplateRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.TemplateId))
            {
                throw new ArgumentException("TemplateId is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Name is required.", nameof(request));
            }

            var (expression, card) = request.TemplateId.Trim() switch
            {
                MaxDoseTemplate => BuildMaxDoseTemplate(request),
                AllergyConflictTemplate => BuildAllergyConflictTemplate(request),
                PregnancyWarningTemplate => BuildPregnancyWarningTemplate(request),
                ControlledMedicationTemplate => BuildControlledMedicationTemplate(request),
                IndicationMismatchTemplate => BuildIndicationMismatchTemplate(request),
                DuplicateIngredientConflictTemplate => BuildDuplicateIngredientConflictTemplate(request),
                HighUtilizationRiskTemplate => BuildHighUtilizationRiskTemplate(request),
                PolypharmacyWarningTemplate => BuildPolypharmacyWarningTemplate(request),
                HighRiskPatientViewTemplate => BuildHighRiskPatientViewTemplate(request),
                _ => throw new ArgumentException($"Unsupported template '{request.TemplateId}'.", nameof(request))
            };

            // patient-view templates always use patient-view hook
            var hookId = request.TemplateId.Trim() == HighRiskPatientViewTemplate
                ? "patient-view"
                : string.IsNullOrWhiteSpace(request.HookId) ? "medication-prescribe" : request.HookId;

            return new CdsRuleDefinition
            {
                Id = Guid.Empty,
                HookId = hookId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Priority = request.Priority,
                IsActive = request.IsActive,
                ExpressionJson = Serialize(expression),
                CardTemplateJson = Serialize(card)
            };
        }

        private static (object expression, object card) BuildMaxDoseTemplate(RuleTemplateRequest request)
        {
            var conditions = new List<object>
            {
                new Dictionary<string, object?>
                {
                    [">"] = new object[]
                    {
                        Var("dose.requestedDailyMg"),
                        Var("dose.maxDailyMg")
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(request.MedicationRxCui))
            {
                conditions.Add(new Dictionary<string, object?>
                {
                    ["=="] = new object[]
                    {
                        Var("medication.rxCui"),
                        request.MedicationRxCui.Trim()
                    }
                });
            }

            return (
                new Dictionary<string, object?> { ["and"] = conditions.ToArray() },
                BuildCard(
                    request,
                    "Dose too high for {{medication.name}}",
                    "Requested {{dose.requestedDailyMg}} mg/day exceeds max {{dose.maxDailyMg}} mg/day."));
        }

        private static (object expression, object card) BuildAllergyConflictTemplate(RuleTemplateRequest request)
        {
            return (
                new Dictionary<string, object?>
                {
                    ["=="] = new object[]
                    {
                        Var("safety.allergyConflict"),
                        true
                    }
                },
                BuildCard(
                    request,
                    "Allergy conflict for {{medication.name}}",
                    "Matched allergy ingredients: {{allergies.matches}}"));
        }

        private static (object expression, object card) BuildPregnancyWarningTemplate(RuleTemplateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PregnancyCategory))
            {
                throw new ArgumentException("PregnancyCategory is required for the pregnancy warning template.", nameof(request));
            }

            var category = request.PregnancyCategory.Trim().ToUpperInvariant();
            return (
                new Dictionary<string, object?>
                {
                    ["=="] = new object[]
                    {
                        Var("safety.pregnancyCategory"),
                        category
                    }
                },
                BuildCard(
                    request,
                    $"Pregnancy warning for {{medication.name}}",
                    $"Medication pregnancy category is {category}."));
        }

        private static (object expression, object card) BuildControlledMedicationTemplate(RuleTemplateRequest request)
        {
            return (
                new Dictionary<string, object?>
                {
                    ["=="] = new object[]
                    {
                        Var("safety.isControlled"),
                        true
                    }
                },
                BuildCard(
                    request,
                    "Controlled medication selected: {{medication.name}}",
                    "This medication is flagged as controlled."));
        }

        private static (object expression, object card) BuildIndicationMismatchTemplate(RuleTemplateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.IndicationCode))
            {
                throw new ArgumentException("IndicationCode is required for the indication mismatch template.", nameof(request));
            }

            var indicationCode = request.IndicationCode.Trim();
            return (
                new Dictionary<string, object?>
                {
                    ["=="] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["in"] = new object[]
                            {
                                indicationCode,
                                Var("medication.indications")
                            }
                        },
                        false
                    }
                },
                BuildCard(
                    request,
                    "Indication mismatch for {{medication.name}}",
                    $"Indication {indicationCode} is not listed for this medication."));
        }

        private static (object expression, object card) BuildDuplicateIngredientConflictTemplate(RuleTemplateRequest request)
        {
            return (
                new Dictionary<string, object?>
                {
                    ["=="] = new object[]
                    {
                        Var("therapy.duplicateIngredientConflict"),
                        true
                    }
                },
                BuildCard(
                    request,
                    "Duplicate ingredient conflict for {{medication.name}}",
                    "Shared ingredients with other active medications: {{therapy.duplicateIngredientMatches}}"));
        }

        private static (object expression, object card) BuildHighUtilizationRiskTemplate(RuleTemplateRequest request)
        {
            object expression = new Dictionary<string, object?>
            {
                ["=="] = new object[]
                {
                    Var("ai.highUtilizationRisk"),
                    true
                }
            };

            if (request.HighUtilizationProbabilityThreshold.HasValue)
            {
                expression = new Dictionary<string, object?>
                {
                    ["and"] = new object[]
                    {
                        expression,
                        new Dictionary<string, object?>
                        {
                            [">="] = new object[]
                            {
                                Var("ai.highUtilizationProbability"),
                                request.HighUtilizationProbabilityThreshold.Value
                            }
                        }
                    }
                };
            }

            return (
                expression,
                BuildCard(
                    request,
                    "High utilization risk for patient {{patient.id}}",
                    "AI model {{ai.highUtilizationModel}} predicts frequent-flyer risk with probability {{ai.highUtilizationProbability}}."));
        }

        private static (object expression, object card) BuildPolypharmacyWarningTemplate(RuleTemplateRequest request)
        {
            return (
                new Dictionary<string, object?>
                {
                    [">="] = new object[]
                    {
                        Var("therapy.activeMedicationCount"),
                        5
                    }
                },
                BuildCard(
                    request,
                    "Polypharmacy risk — {{therapy.activeMedicationCount}} active medications",
                    "This patient is on 5 or more concurrent active medications. Review for potential drug interactions and deprescribing opportunities."));
        }

        private static (object expression, object card) BuildHighRiskPatientViewTemplate(RuleTemplateRequest request)
        {
            return (
                new Dictionary<string, object?>
                {
                    ["=="] = new object[]
                    {
                        Var("ai.highUtilizationRisk"),
                        true
                    }
                },
                BuildCard(
                    request,
                    "High-utilization risk patient — {{ai.riskLevel}} ({{ai.compositeScore}})",
                    "This patient has been flagged by the AI risk model. Key factors: chronic conditions={{ai.hasChronicConditions}}, condition count={{ai.conditionCount}}, medication count={{ai.medicationCount}}."));
        }

        private static Dictionary<string, object?> BuildCard(
            RuleTemplateRequest request,
            string summary,
            string detail)
        {
            return new Dictionary<string, object?>
            {
                ["summary"] = summary,
                ["detail"] = detail,
                ["indicator"] = string.IsNullOrWhiteSpace(request.Indicator) ? "warning" : request.Indicator.Trim(),
                ["source"] = new Dictionary<string, object?>
                {
                    ["label"] = string.IsNullOrWhiteSpace(request.SourceLabel) ? "DMRS CDS" : request.SourceLabel.Trim(),
                    ["url"] = request.SourceUrl?.Trim()
                }
            };
        }

        private static Dictionary<string, object?> Var(string path)
            => new()
            {
                ["var"] = path
            };

        private static string Serialize(object value)
            => JsonSerializer.Serialize(value);
    }
}
