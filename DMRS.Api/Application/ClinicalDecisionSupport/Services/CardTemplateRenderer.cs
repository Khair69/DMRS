using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class CardTemplateRenderer : ICardTemplateRenderer
    {
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

                var summary = GetString(root, "summary") ?? "CDS advisory";
                var detail = GetString(root, "detail");
                var indicator = GetString(root, "indicator") ?? "info";

                var source = GetSource(root);
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

        private static CdsCardSource GetSource(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("source", out var sourceElement)
                || sourceElement.ValueKind != JsonValueKind.Object)
            {
                return new CdsCardSource("DMRS CDS", null);
            }

            var label = GetString(sourceElement, "label") ?? "DMRS CDS";
            var url = GetString(sourceElement, "url");

            return new CdsCardSource(label, url);
        }
    }
}
