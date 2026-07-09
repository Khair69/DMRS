using DMRS.Api.Application.ExternalAi.Interfaces;
using DMRS.Api.Application.ExternalAi.Models;
using DMRS.Api.Domain.ExternalAi;

namespace DMRS.Api.Application.ExternalAi.Services
{
    /// <summary>
    /// CRUD for the external AI model registry. Validates input (HTTPS-only endpoints, auth needs a
    /// secret), encrypts the secret at rest, and maps to a secret-free DTO for clients.
    /// </summary>
    public sealed class ExternalAiModelManagementService : IExternalAiModelManagementService
    {
        private const int MinTimeoutSeconds = 1;
        private const int MaxTimeoutSeconds = 300;

        private readonly IExternalAiModelRepository _repository;
        private readonly IExternalAiSecretProtector _secretProtector;

        public ExternalAiModelManagementService(
            IExternalAiModelRepository repository,
            IExternalAiSecretProtector secretProtector)
        {
            _repository = repository;
            _secretProtector = secretProtector;
        }

        public async Task<IReadOnlyList<ExternalAiModelDto>> ListAsync(CancellationToken cancellationToken)
        {
            var models = await _repository.ListAsync(cancellationToken);
            return models.Select(ExternalAiModelDto.FromEntity).ToList();
        }

        public async Task<IReadOnlyList<ExternalAiModelSummaryDto>> ListActiveSummariesAsync(CancellationToken cancellationToken)
        {
            var models = await _repository.ListAsync(cancellationToken);
            return models
                .Where(m => m.IsActive)
                .Select(ExternalAiModelSummaryDto.FromEntity)
                .ToList();
        }

        public async Task<ExternalAiModelDto?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            var model = await _repository.GetByIdAsync(id, cancellationToken);
            return model is null ? null : ExternalAiModelDto.FromEntity(model);
        }

        public async Task<ExternalAiModelDto> CreateAsync(ExternalAiModelInput input, string? userName, CancellationToken cancellationToken)
        {
            Validate(input, isCreate: true);

            var now = DateTimeOffset.UtcNow;
            var model = new ExternalAiModel
            {
                Id = Guid.NewGuid(),
                CreatedBy = userName,
                CreatedAt = now,
                UpdatedBy = userName,
                UpdatedAt = now
            };

            ApplyInput(model, input);
            await _repository.AddAsync(model, cancellationToken);
            return ExternalAiModelDto.FromEntity(model);
        }

        public async Task<ExternalAiModelDto?> UpdateAsync(Guid id, ExternalAiModelInput input, string? userName, CancellationToken cancellationToken)
        {
            var model = await _repository.GetByIdAsync(id, cancellationToken);
            if (model is null)
            {
                return null;
            }

            Validate(input, isCreate: false);

            ApplyInput(model, input);
            model.UpdatedBy = userName;
            model.UpdatedAt = DateTimeOffset.UtcNow;

            await _repository.UpdateAsync(model, cancellationToken);
            return ExternalAiModelDto.FromEntity(model);
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            var model = await _repository.GetByIdAsync(id, cancellationToken);
            if (model is null)
            {
                return false;
            }

            await _repository.DeleteAsync(model, cancellationToken);
            return true;
        }

        private void ApplyInput(ExternalAiModel model, ExternalAiModelInput input)
        {
            model.Name = input.Name.Trim();
            model.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
            model.EndpointUrl = input.EndpointUrl.Trim();
            model.AuthType = input.AuthType;
            model.AuthHeaderName = input.AuthType == ExternalAiAuthType.ApiKey
                ? (string.IsNullOrWhiteSpace(input.AuthHeaderName) ? "X-API-Key" : input.AuthHeaderName.Trim())
                : null;
            model.TimeoutSeconds = input.TimeoutSeconds;
            model.DecisionJsonPath = string.IsNullOrWhiteSpace(input.DecisionJsonPath) ? null : input.DecisionJsonPath.Trim();
            model.IsActive = input.IsActive;

            if (input.AuthType == ExternalAiAuthType.None)
            {
                // Switching to no-auth clears any stored secret.
                model.EncryptedSecret = null;
            }
            else if (!string.IsNullOrWhiteSpace(input.Secret))
            {
                // A provided secret (re)sets it; a null/blank secret on update keeps the existing one.
                model.EncryptedSecret = _secretProtector.Protect(input.Secret.Trim());
            }
        }

        private static void Validate(ExternalAiModelInput input, bool isCreate)
        {
            if (string.IsNullOrWhiteSpace(input.Name))
            {
                throw new ExternalAiModelValidationException("Name is required.");
            }

            if (string.IsNullOrWhiteSpace(input.EndpointUrl)
                || !Uri.TryCreate(input.EndpointUrl.Trim(), UriKind.Absolute, out var uri))
            {
                throw new ExternalAiModelValidationException("Endpoint URL must be a valid absolute URL.");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ExternalAiModelValidationException("Endpoint URL must use HTTPS — patient data may not be sent over an unencrypted connection.");
            }

            if (input.TimeoutSeconds is < MinTimeoutSeconds or > MaxTimeoutSeconds)
            {
                throw new ExternalAiModelValidationException($"Timeout must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds} seconds.");
            }

            // Auth requires a secret. On create it must be supplied; on update a blank secret means
            // "keep the existing one", so we can't reject it here without knowing the stored state.
            if (isCreate && input.AuthType != ExternalAiAuthType.None && string.IsNullOrWhiteSpace(input.Secret))
            {
                throw new ExternalAiModelValidationException("A secret (API key or token) is required for the selected authentication type.");
            }
        }
    }
}
