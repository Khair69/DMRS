namespace DMRS.Api.Domain
{
    public sealed class DrugMapping
    {
        public Guid Id { get; set; }
        public string SourceTerm { get; set; } = string.Empty;
        public string SourceSystem { get; set; } = string.Empty;
        public string IngredientRxCui { get; set; } = string.Empty;
        public DateTimeOffset LastUpdatedUtc { get; set; }
    }
}
