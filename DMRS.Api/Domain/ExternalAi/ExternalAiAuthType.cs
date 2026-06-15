namespace DMRS.Api.Domain.ExternalAi
{
    /// <summary>
    /// How DMRS authenticates to a registered external AI model endpoint when sending patient data.
    /// </summary>
    public enum ExternalAiAuthType
    {
        /// <summary>No authentication header is sent.</summary>
        None = 0,

        /// <summary>The secret is sent verbatim in a custom header (e.g. <c>X-API-Key: {secret}</c>).</summary>
        ApiKey = 1,

        /// <summary>The secret is sent as a bearer token (<c>Authorization: Bearer {secret}</c>).</summary>
        Bearer = 2
    }
}
