namespace DMRS.Api.Infrastructure.ClinicalDecisionSupport
{
    public interface IRxNormClient
    {
        Task<string?> GetRxCuiByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<string?> GetRxCuiByNdcAsync(string ndc, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetIngredientRxCuisAsync(string rxcui, CancellationToken cancellationToken = default);
    }
}
