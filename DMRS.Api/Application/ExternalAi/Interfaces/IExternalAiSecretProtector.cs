namespace DMRS.Api.Application.ExternalAi.Interfaces
{
    /// <summary>
    /// Encrypts/decrypts external-model secrets (API keys, bearer tokens) at rest. Wraps the ASP.NET
    /// Data Protection API with a fixed purpose so management (protect) and inference (unprotect) agree.
    /// </summary>
    public interface IExternalAiSecretProtector
    {
        string Protect(string plaintext);

        /// <summary>Returns null if the value cannot be decrypted (e.g. key ring rotated away).</summary>
        string? Unprotect(string? protectedValue);
    }
}
