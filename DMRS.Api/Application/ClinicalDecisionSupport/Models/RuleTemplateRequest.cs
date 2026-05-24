namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class RuleTemplateRequest
    {
        public string TemplateId { get; set; } = string.Empty;
        public string HookId { get; set; } = "medication-prescribe";
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Priority { get; set; }
        public bool IsActive { get; set; } = true;
        public string Indicator { get; set; } = "warning";
        public string SourceLabel { get; set; } = "DMRS CDS";
        public string? SourceUrl { get; set; }
        public string? MedicationRxCui { get; set; }
        public string? PregnancyCategory { get; set; }
        public string? IndicationCode { get; set; }
        public float? HighUtilizationProbabilityThreshold { get; set; }
    }
}
