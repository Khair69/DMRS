using DMRS.Api.Domain.ExternalAi;

namespace DMRS.Api.Application.ExternalAi.Models
{
    /// <summary>
    /// Create/update payload for a registered external AI model. <see cref="Secret"/> is the plaintext
    /// API key / token as typed by the admin; the management service encrypts it before persisting.
    /// On update, a null <see cref="Secret"/> leaves the stored secret unchanged.
    /// </summary>
    public sealed class ExternalAiModelInput
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string EndpointUrl { get; set; } = string.Empty;

        public ExternalAiAuthType AuthType { get; set; } = ExternalAiAuthType.None;

        public string? AuthHeaderName { get; set; }

        public string? Secret { get; set; }

        public int TimeoutSeconds { get; set; } = 30;

        public string? DecisionJsonPath { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
