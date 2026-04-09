using System.Text.Json;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record CdsHookRequest(
        string Hook,
        string HookInstance,
        JsonElement Context,
        JsonElement? Prefetch);
}
