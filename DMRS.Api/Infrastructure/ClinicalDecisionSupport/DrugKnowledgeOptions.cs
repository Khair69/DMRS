namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public sealed class DrugKnowledgeOptions
    {
        public const string SectionName = "Cds:DrugKnowledge";

        public List<DrugKnowledgeEntry> Entries { get; set; } = [];
    }

    public sealed class DrugKnowledgeEntry
    {
        public string Code { get; set; } = string.Empty;
        public string? Display { get; set; }
        public double MaxDailyDoseMg { get; set; }
    }
}
