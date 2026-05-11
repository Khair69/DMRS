using System.Text.Json;
using System.Text.RegularExpressions;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class CardTemplateRenderer : ICardTemplateRenderer
    {
        private static readonly Regex PlaceholderRegex = new("{{\\s*([\\w\\.]+)\\s*}}", RegexOptions.Compiled);

        public CdsCard Render(string cardTemplateJson, CdsContext context)
        {
            if (string.IsNullOrWhiteSpace(cardTemplateJson))
            {
                return DefaultCard();
            }

            try
            {
                using var doc = JsonDocument.Parse(cardTemplateJson);
                var root = doc.RootElement;

                var summary = Interpolate(GetString(root, "summary") ?? "CDS advisory", context) ?? "CDS advisory";
                var detail = Interpolate(GetString(root, "detail"), context);
                var indicator = Interpolate(GetString(root, "indicator") ?? "info", context) ?? "info";

                var source = GetSource(root, context);
                return new CdsCard(summary, detail, indicator, source);
            }
            catch (JsonException)
            {
                return DefaultCard();
            }
        }

        private static CdsCard DefaultCard()
        {
            return new CdsCard(
                "CDS advisory",
                null,
                "info",
                new CdsCardSource("DMRS CDS", null));
        }

        private static string? GetString(JsonElement element, string name)
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty(name, out var property)
                && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            return null;
        }

        private static CdsCardSource GetSource(JsonElement root, CdsContext context)
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("source", out var sourceElement)
                || sourceElement.ValueKind != JsonValueKind.Object)
            {
                return new CdsCardSource("DMRS CDS", null);
            }

            var label = Interpolate(GetString(sourceElement, "label") ?? "DMRS CDS", context) ?? "DMRS CDS";
            var url = Interpolate(GetString(sourceElement, "url"), context);

            return new CdsCardSource(label, url);
        }

        private static string? Interpolate(string? template, CdsContext context)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return template;
            }

            return PlaceholderRegex.Replace(template, match =>
            {
                var path = match.Groups[1].Value;
                return TryResolve(path, context.Data, out var value) ? value : string.Empty;
            });
        }

        private static bool TryResolve(string path, IReadOnlyDictionary<string, object?> data, out string value)
        {
            value = string.Empty;

            var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || !data.TryGetValue(segments[0], out var current))
            {
                return false;
            }

            for (var i = 1; i < segments.Length; i++)
            {
                current = Traverse(current, segments[i]);
                if (current == null)
                {
                    return false;
                }
            }

            value = ToDisplayString(current);
            return true;
        }

        private static object? Traverse(object? current, string segment)
        {
            if (current is JsonElement json)
            {
                return json.ValueKind == JsonValueKind.Object && json.TryGetProperty(segment, out var child)
                    ? child
                    : null;
            }

            if (current is IReadOnlyDictionary<string, object?> dict && dict.TryGetValue(segment, out var next))
            {
                return next;
            }

            if (current is IDictionary<string, object?> mutableDict && mutableDict.TryGetValue(segment, out next))
            {
                return next;
            }

            return null;
        }

        private static string ToDisplayString(object? value)
        {
            return value switch
            {
                null => string.Empty,
                string s => s,
                bool b => b ? "true" : "false",
                JsonElement json => json.ValueKind switch
                {
                    JsonValueKind.String => json.GetString() ?? string.Empty,
                    JsonValueKind.Number => json.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Array => string.Join(", ", json.EnumerateArray().Select(item => ToDisplayString(item))),
                    _ => json.ToString()
                },
                IEnumerable<string> strings => string.Join(", ", strings),
                IEnumerable<object?> objects => string.Join(", ", objects.Select(ToDisplayString)),
                _ => Convert.ToString(value) ?? string.Empty
            };
        }
    }
}
