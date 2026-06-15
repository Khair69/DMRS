using DMRS.Api.Application.ExternalAi.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace DMRS.Api.Infrastructure.ExternalAi
{
    public sealed class ExternalAiSecretProtector : IExternalAiSecretProtector
    {
        private const string Purpose = "DMRS.ExternalAi.ModelSecret.v1";

        private readonly IDataProtector _protector;

        public ExternalAiSecretProtector(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector(Purpose);
        }

        public string Protect(string plaintext) => _protector.Protect(plaintext);

        public string? Unprotect(string? protectedValue)
        {
            if (string.IsNullOrEmpty(protectedValue))
            {
                return null;
            }

            try
            {
                return _protector.Unprotect(protectedValue);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return null;
            }
        }
    }
}
