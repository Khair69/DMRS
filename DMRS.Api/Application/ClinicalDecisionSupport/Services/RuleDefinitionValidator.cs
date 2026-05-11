using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using DMRS.Api.Domain.ClinicalDecisionSupport;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class RuleDefinitionValidator : IRuleDefinitionValidator
    {
        private static readonly HashSet<string> SupportedOperators =
        [
            "var",
            "==",
            "!=",
            ">",
            "<",
            ">=",
            "<=",
            "and",
            "or",
            "in"
        ];

        public RuleValidationResult Validate(CdsRuleDefinition rule)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(rule.HookId))
            {
                errors.Add("HookId is required.");
            }

            if (string.IsNullOrWhiteSpace(rule.Name))
            {
                errors.Add("Name is required.");
            }

            ValidateJson(rule.ExpressionJson, "ExpressionJson", errors, ValidateExpressionOperators);
            ValidateJson(rule.CardTemplateJson, "CardTemplateJson", errors, null);

            return new RuleValidationResult(errors.Count == 0, errors);
        }

        private static void ValidateJson(
            string json,
            string fieldName,
            List<string> errors,
            Action<JsonElement, List<string>>? extraValidation)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                errors.Add($"{fieldName} is required.");
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                extraValidation?.Invoke(doc.RootElement, errors);
            }
            catch (JsonException ex)
            {
                errors.Add($"{fieldName} is not valid JSON: {ex.Message}");
            }
        }

        private static void ValidateExpressionOperators(JsonElement element, List<string> errors)
        {
            Traverse(element, errors);
        }

        private static void Traverse(JsonElement element, List<string> errors)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    Traverse(item, errors);
                }

                return;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var properties = element.EnumerateObject().ToArray();
            if (properties.Length == 1)
            {
                var op = properties[0].Name;
                if (!SupportedOperators.Contains(op) && op != "summary" && op != "detail" && op != "indicator" && op != "source" && op != "label" && op != "url")
                {
                    errors.Add($"Unsupported operator or property '{op}'.");
                }
            }

            foreach (var property in properties)
            {
                Traverse(property.Value, errors);
            }
        }
    }
}
