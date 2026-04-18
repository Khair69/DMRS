namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class KnowledgeCacheOptions
    {
        public int CacheTtlDays { get; set; } = 30;
        public string Provider { get; set; } = "MockMedicineApi";
    }
}
