using DMRS.Api.Application.ExternalAi.Models;

namespace DMRS.Api.Application.ExternalAi.Interfaces
{
    public interface IExternalAiModelManagementService
    {
        Task<IReadOnlyList<ExternalAiModelDto>> ListAsync(CancellationToken cancellationToken);

        Task<ExternalAiModelDto?> GetAsync(Guid id, CancellationToken cancellationToken);

        Task<ExternalAiModelDto> CreateAsync(ExternalAiModelInput input, string? userName, CancellationToken cancellationToken);

        /// <summary>Returns null when no model with <paramref name="id"/> exists.</summary>
        Task<ExternalAiModelDto?> UpdateAsync(Guid id, ExternalAiModelInput input, string? userName, CancellationToken cancellationToken);

        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
    }

    /// <summary>Thrown when a create/update request fails validation; surfaced as 400 by the controller.</summary>
    public sealed class ExternalAiModelValidationException : Exception
    {
        public ExternalAiModelValidationException(string message) : base(message) { }
    }
}
