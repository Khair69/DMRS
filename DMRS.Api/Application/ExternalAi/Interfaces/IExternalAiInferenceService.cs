using DMRS.Api.Application.ExternalAi.Models;

namespace DMRS.Api.Application.ExternalAi.Interfaces
{
    public interface IExternalAiInferenceService
    {
        /// <summary>
        /// Sends the patient's FHIR data to the registered model and returns its decision. Returns null
        /// only when the model id is unknown or inactive; transport/HTTP failures come back as an
        /// unsuccessful <see cref="ExternalAiInferenceResult"/> (never thrown), so the UI can show why.
        /// </summary>
        Task<ExternalAiInferenceResult?> RunAsync(Guid modelId, string patientId, CancellationToken cancellationToken);
    }
}
