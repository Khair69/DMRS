namespace DMRS.Api.Domain
{
    public sealed class DrugMaxDose
    {
        public Guid Id { get; set; }
        public string IngredientRxCui { get; set; } = string.Empty;
        public double MaxDailyDoseMg { get; set; }
        public string? Display { get; set; }
        public DateTimeOffset LastUpdatedUtc { get; set; }
    }
}
