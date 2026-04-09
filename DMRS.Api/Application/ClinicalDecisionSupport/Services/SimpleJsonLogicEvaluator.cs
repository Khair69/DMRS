using System.Globalization;
using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class SimpleJsonLogicEvaluator : IRuleExpressionEvaluator
    {
        public bool Evaluate(string expressionJson, CdsContext context)
        {
            if (string.IsNullOrWhiteSpace(expressionJson))
            {
                return false;
            }

            using var doc = JsonDocument.Parse(expressionJson);
            var result = EvaluateElement(doc.RootElement, context);
            return ToBoolean(result);
        }

        private static object? EvaluateElement(JsonElement element, CdsContext context)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Number:
                    if (element.TryGetDecimal(out var dec))
                    {
                        return dec;
                    }
                    return element.GetDouble();
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.Array:
                    return element.EnumerateArray().Select(e => EvaluateElement(e, context)).ToList();
                case JsonValueKind.Object:
                    return EvaluateObject(element, context);
                default:
                    return null;
            }
        }

        private static object? EvaluateObject(JsonElement element, CdsContext context)
        {
            var properties = element.EnumerateObject().ToList();
            if (properties.Count != 1)
            {
                return element;
            }

            var op = properties[0].Name;
            var value = properties[0].Value;

            return op switch
            {
                "var" => ResolveVar(value, context),
                "==" => Compare(value, context, (a, b) => Equals(a, b)),
                "!=" => Compare(value, context, (a, b) => !Equals(a, b)),
                ">" => CompareNumeric(value, context, (a, b) => a > b),
                "<" => CompareNumeric(value, context, (a, b) => a < b),
                ">=" => CompareNumeric(value, context, (a, b) => a >= b),
                "<=" => CompareNumeric(value, context, (a, b) => a <= b),
                "and" => EvaluateLogical(value, context, true),
                "or" => EvaluateLogical(value, context, false),
                "in" => EvaluateIn(value, context),
                _ => null
            };
        }

        private static object? ResolveVar(JsonElement value, CdsContext context)
        {
            string? path = null;
            object? fallback = null;

            if (value.ValueKind == JsonValueKind.String)
            {
                path = value.GetString();
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                var args = value.EnumerateArray().ToArray();
                if (args.Length > 0 && args[0].ValueKind == JsonValueKind.String)
                {
                    path = args[0].GetString();
                }
                if (args.Length > 1)
                {
                    fallback = EvaluateElement(args[1], context);
                }
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return fallback;
            }

            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return fallback;
            }

            if (!context.Data.TryGetValue(segments[0], out var current))
            {
                return fallback;
            }

            for (var i = 1; i < segments.Length; i++)
            {
                current = Traverse(current, segments[i]);
                if (current == null)
                {
                    return fallback;
                }
            }

            return current ?? fallback;
        }

        private static object? Traverse(object? current, string segment)
        {
            if (current == null)
            {
                return null;
            }

            if (current is JsonElement jsonElement)
            {
                if (jsonElement.ValueKind == JsonValueKind.Object
                    && jsonElement.TryGetProperty(segment, out var child))
                {
                    return child;
                }

                return null;
            }

            if (current is IReadOnlyDictionary<string, object?> dict
                && dict.TryGetValue(segment, out var value))
            {
                return value;
            }

            return null;
        }

        private static bool EvaluateLogical(JsonElement value, CdsContext context, bool requireAll)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var results = value.EnumerateArray()
                .Select(e => ToBoolean(EvaluateElement(e, context)))
                .ToList();

            return requireAll ? results.All(r => r) : results.Any(r => r);
        }

        private static bool EvaluateIn(JsonElement value, CdsContext context)
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var args = value.EnumerateArray().ToArray();
            if (args.Length < 2)
            {
                return false;
            }

            var needle = EvaluateElement(args[0], context);
            var haystack = EvaluateElement(args[1], context);

            if (haystack is string haystackString && needle is string needleString)
            {
                return haystackString.Contains(needleString, StringComparison.OrdinalIgnoreCase);
            }

            if (haystack is IEnumerable<object?> list)
            {
                return list.Any(item => Equals(item, needle));
            }

            return false;
        }

        private static bool Compare(JsonElement value, CdsContext context, Func<object?, object?, bool> comparer)
        {
            var args = ExtractArguments(value, context);
            return args.Count >= 2 && comparer(args[0], args[1]);
        }

        private static bool CompareNumeric(JsonElement value, CdsContext context, Func<decimal, decimal, bool> comparer)
        {
            var args = ExtractArguments(value, context);
            if (args.Count < 2)
            {
                return false;
            }

            if (!TryToDecimal(args[0], out var left) || !TryToDecimal(args[1], out var right))
            {
                return false;
            }

            return comparer(left, right);
        }

        private static List<object?> ExtractArguments(JsonElement value, CdsContext context)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                return value.EnumerateArray().Select(e => EvaluateElement(e, context)).ToList();
            }

            return [EvaluateElement(value, context)];
        }

        private static bool ToBoolean(object? value)
        {
            return value switch
            {
                null => false,
                bool b => b,
                string s => !string.IsNullOrWhiteSpace(s),
                decimal d => d != 0,
                double d => Math.Abs(d) > double.Epsilon,
                int i => i != 0,
                long l => l != 0,
                IEnumerable<object?> e => e.Any(),
                JsonElement json => json.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => !string.IsNullOrWhiteSpace(json.GetString()),
                    JsonValueKind.Number => json.TryGetDecimal(out var dec) ? dec != 0 : json.GetDouble() != 0,
                    JsonValueKind.Array => json.EnumerateArray().Any(),
                    JsonValueKind.Object => json.EnumerateObject().Any(),
                    _ => false
                },
                _ => true
            };
        }

        private static bool TryToDecimal(object? value, out decimal number)
        {
            switch (value)
            {
                case decimal dec:
                    number = dec;
                    return true;
                case double dbl:
                    number = Convert.ToDecimal(dbl, CultureInfo.InvariantCulture);
                    return true;
                case int i:
                    number = i;
                    return true;
                case long l:
                    number = l;
                    return true;
                case string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed):
                    number = parsed;
                    return true;
                case JsonElement json when json.ValueKind == JsonValueKind.Number:
                    if (json.TryGetDecimal(out var jsonDec))
                    {
                        number = jsonDec;
                        return true;
                    }
                    number = Convert.ToDecimal(json.GetDouble(), CultureInfo.InvariantCulture);
                    return true;
                default:
                    number = 0;
                    return false;
            }
        }
    }
}
