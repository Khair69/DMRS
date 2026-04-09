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
        public string ExpressionJson { get; set; } = string.Empty;
        public string CardTemplateJson { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
