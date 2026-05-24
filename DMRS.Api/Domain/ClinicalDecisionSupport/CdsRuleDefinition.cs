namespace DMRS.Api.Domain.ClinicalDecisionSupport
{
    public class CdsRuleDefinition
    {
        public Guid Id { get; set; }
        public string HookId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; }
        public CdsRuleStatus Status { get; set; } = CdsRuleStatus.Draft;
        public bool HasUnpublishedChanges { get; set; }
        public Guid? PublishedVersionId { get; set; }
        public int? PublishedVersionNumber { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
        public string? PublishedBy { get; set; }
        public string ExpressionJson { get; set; } = string.Empty;
        public string CardTemplateJson { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
