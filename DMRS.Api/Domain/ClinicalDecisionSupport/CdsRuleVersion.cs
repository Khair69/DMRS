namespace DMRS.Api.Domain.ClinicalDecisionSupport
{
    public sealed class CdsRuleVersion
    {
        public Guid Id { get; set; }
        public Guid RuleDefinitionId { get; set; }
        public int VersionNumber { get; set; }
        public string HookId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public string ExpressionJson { get; set; } = string.Empty;
        public string CardTemplateJson { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public string? PublishedBy { get; set; }
    }
}
