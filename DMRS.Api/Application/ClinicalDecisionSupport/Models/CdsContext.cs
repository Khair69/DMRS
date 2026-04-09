using System.Collections.ObjectModel;
using System.Text.Json;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record CdsContext(
        string HookId,
        string HookInstance,
        string? PatientId,
        IReadOnlyDictionary<string, object?> Data,
        JsonElement RawContext,
        JsonElement? Prefetch)
    {
        public static CdsContext Create(
            string hookId,
            string hookInstance,
            string? patientId,
            IDictionary<string, object?> data,
            JsonElement rawContext,
            JsonElement? prefetch)
        {
            var readOnly = new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(data));
            return new CdsContext(hookId, hookInstance, patientId, readOnly, rawContext, prefetch);
        }
    }
}
