namespace DMRS.Api.Domain.ExternalAi
{
    /// <summary>
    /// A remotely-hosted ("away") AI model registered by an admin. DMRS POSTs a patient's FHIR data
    /// to <see cref="EndpointUrl"/> and reads the model's decision back from the JSON response. This
    /// is the persisted registry record; it never leaves the API with its secret attached (see the
    /// management service, which maps to a secret-free response DTO).
    /// </summary>
    public class ExternalAiModel
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>HTTPS endpoint the patient FHIR bundle is POSTed to. Enforced HTTPS-only on save.</summary>
        public string EndpointUrl { get; set; } = string.Empty;

        public ExternalAiAuthType AuthType { get; set; } = ExternalAiAuthType.None;

        /// <summary>
        /// Header name used to carry the secret when <see cref="AuthType"/> is <see cref="ExternalAiAuthType.ApiKey"/>.
        /// Ignored for <see cref="ExternalAiAuthType.Bearer"/> (always <c>Authorization</c>) and <see cref="ExternalAiAuthType.None"/>.
        /// </summary>
        public string? AuthHeaderName { get; set; }

        /// <summary>
        /// The API key / bearer token encrypted at rest with the ASP.NET Data Protection API. Never
        /// returned to clients. Null when <see cref="AuthType"/> is <see cref="ExternalAiAuthType.None"/>.
        /// </summary>
        public string? EncryptedSecret { get; set; }

        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Optional dot-separated path into the response JSON to surface as the decision
        /// (e.g. <c>result.label</c>). When null, the whole response body is returned.
        /// </summary>
        public string? DecisionJsonPath { get; set; }

        public bool IsActive { get; set; } = true;

        public string? CreatedBy { get; set; }

        public string? UpdatedBy { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
