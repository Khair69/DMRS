using System.Text.Json.Serialization;

namespace DMRS.Api.Domain.ClinicalDecisionSupport
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CdsRuleStatus
    {
        Draft = 0,
        Published = 1,
        Archived = 2
    }
}
