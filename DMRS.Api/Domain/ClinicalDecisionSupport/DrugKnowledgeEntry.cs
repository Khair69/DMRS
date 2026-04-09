namespace DMRS.Api.Domain.ClinicalDecisionSupport
{
    public class DrugKnowledgeEntry
    {
        public Guid Id { get; set; }
        public string QueryKey { get; set; } = string.Empty;
        public string KnowledgeType { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTimeOffset FetchedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
