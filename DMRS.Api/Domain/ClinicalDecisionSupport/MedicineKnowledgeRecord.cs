namespace DMRS.Api.Domain.ClinicalDecisionSupport
{
    public sealed class MedicineKnowledgeRecord
    {
        public string RxCui { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal? MaxDailyMg { get; set; }
        public decimal? MaxSingleMg { get; set; }
        public decimal? WarningThresholdMg { get; set; }
        public string? PregnancyCategory { get; set; }
        public bool? IsControlled { get; set; }
        public string IngredientCodesJson { get; set; } = "[]";
        public string IngredientNamesJson { get; set; } = "[]";
        public string IndicationCodesJson { get; set; } = "[]";
        public string IngredientSearchText { get; set; } = string.Empty;
        public string IndicationSearchText { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTimeOffset FetchedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
