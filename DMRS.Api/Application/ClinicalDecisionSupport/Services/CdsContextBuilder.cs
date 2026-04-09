using System.Text.Json;
using DMRS.Api.Application.ClinicalDecisionSupport.Interfaces;
using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    public sealed class CdsContextBuilder : ICdsContextBuilder
    {
        public Task<CdsContext> BuildAsync(CdsHookRequest request, CancellationToken cancellationToken)
        {
            var patientId = ExtractPatientId(request.Context);
            var data = new Dictionary<string, object?>
            {
                ["hook"] = request.Hook,
                ["hookInstance"] = request.HookInstance,
                ["patientId"] = patientId,
                ["context"] = request.Context,
                ["prefetch"] = request.Prefetch
            };

            var context = CdsContext.Create(
                request.Hook,
                request.HookInstance,
                patientId,
                data,
                request.Context,
                request.Prefetch);

            return Task.FromResult(context);
        }

        private static string? ExtractPatientId(JsonElement context)
        {
            if (context.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (context.TryGetProperty("patientId", out var patientIdElement)
                && patientIdElement.ValueKind == JsonValueKind.String)
            {
                return patientIdElement.GetString();
            }

            if (context.TryGetProperty("patient", out var patientElement)
                && patientElement.ValueKind == JsonValueKind.String)
            {
                return patientElement.GetString();
            }

            return null;
        }
    }
}
