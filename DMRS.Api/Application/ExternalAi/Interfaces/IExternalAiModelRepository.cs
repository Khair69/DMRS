using DMRS.Api.Domain.ExternalAi;

namespace DMRS.Api.Application.ExternalAi.Interfaces
{
    /// <summary>Persistence for the registry of external ("away") AI model endpoints.</summary>
    public interface IExternalAiModelRepository
    {
        Task<IReadOnlyList<ExternalAiModel>> ListAsync(CancellationToken cancellationToken);

        Task<ExternalAiModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        Task AddAsync(ExternalAiModel model, CancellationToken cancellationToken);

        Task UpdateAsync(ExternalAiModel model, CancellationToken cancellationToken);

        Task DeleteAsync(ExternalAiModel model, CancellationToken cancellationToken);
    }
}
