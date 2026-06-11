using System.Collections.Concurrent;
using Microsoft.ML.OnnxRuntime;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    /// <summary>
    /// Singleton cache of ONNX <see cref="InferenceSession"/>s keyed by model file path.
    /// Loading and parsing an ONNX model is expensive and must happen ONCE per model, not per
    /// request. The risk services are registered scoped (they depend on the scoped FHIR
    /// repository / DbContext), so without this pool each request would construct a fresh
    /// <see cref="InferenceSession"/> and re-read the model from disk — which made the dashboard,
    /// that scores ~100 patients per load, spend most of its time reloading the same model.
    /// <see cref="InferenceSession.Run"/> is thread-safe, so one shared session serves all requests.
    /// </summary>
    public sealed class OnnxModelPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, Lazy<InferenceSession>> _sessions =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns the cached session for the model, loading it on first use.</summary>
        public InferenceSession GetOrLoad(string modelPath) =>
            _sessions.GetOrAdd(
                modelPath,
                path => new Lazy<InferenceSession>(
                    () => new InferenceSession(path),
                    LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        public void Dispose()
        {
            foreach (var session in _sessions.Values)
            {
                if (session.IsValueCreated)
                {
                    session.Value.Dispose();
                }
            }
        }
    }
}
