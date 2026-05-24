namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record MedicineKnowledge(
        string RxCui,
        string Name,
        decimal? MaxDailyMg,
        decimal? MaxSingleMg,
        decimal? WarningThresholdMg,
        string? PregnancyCategory,
        bool? IsControlled,
        IReadOnlyList<MedicineIngredient> Ingredients,
        IReadOnlyList<string> Indications,
        string Source,
        DateTimeOffset FetchedAt,
        DateTimeOffset ExpiresAt);

    public sealed record MedicineIngredient(
        string Code,
        string Name);
}
