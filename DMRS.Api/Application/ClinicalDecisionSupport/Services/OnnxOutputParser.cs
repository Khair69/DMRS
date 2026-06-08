using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Services
{
    /// <summary>
    /// Shared parsing for the outputs of scikit-learn classifiers exported to ONNX via skl2onnx.
    /// Such models emit a label tensor (int64/int32) plus a probability output that is either a
    /// float tensor or the ZipMap form (a sequence of <c>Dictionary&lt;long,float&gt;</c>). All DMRS
    /// risk services share this so the parsing only lives in one place.
    /// </summary>
    public static class OnnxOutputParser
    {
        /// <summary>Reads the predicted label and positive-class probability from a model run.</summary>
        public static (bool? label, float? probability) ParseOutputs(IEnumerable<DisposableNamedOnnxValue> outputs)
        {
            bool? label = null;
            float? probability = null;

            foreach (var output in outputs)
            {
                if (TryReadLabel(output, out var parsedLabel))
                {
                    label ??= parsedLabel;
                }

                if (TryReadProbability(output, out var parsedProbability))
                {
                    probability ??= parsedProbability;
                }
            }

            return (label, probability);
        }

        private static bool TryReadLabel(DisposableNamedOnnxValue output, out bool label)
        {
            label = false;

            try
            {
                if (output.AsTensor<long>() is Tensor<long> longTensor && longTensor.Length > 0)
                {
                    label = longTensor.ToArray()[0] != 0;
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                if (output.AsTensor<int>() is Tensor<int> intTensor && intTensor.Length > 0)
                {
                    label = intTensor.ToArray()[0] != 0;
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryReadProbability(DisposableNamedOnnxValue output, out float probability)
        {
            probability = 0;

            try
            {
                if (output.AsTensor<float>() is Tensor<float> floatTensor && floatTensor.Length > 0)
                {
                    var values = floatTensor.ToArray();
                    probability = values.Length == 1 ? values[0] : values[^1];
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var enumerable = output.AsEnumerable<Dictionary<long, float>>();
                var map = enumerable.FirstOrDefault();
                if (map != null)
                {
                    probability = map.TryGetValue(1, out var positiveProbability)
                        ? positiveProbability
                        : map.Values.DefaultIfEmpty(0).Max();
                    return true;
                }
            }
            catch
            {
            }

            try
            {
                var enumerable = output.AsEnumerable<Dictionary<int, float>>();
                var map = enumerable.FirstOrDefault();
                if (map != null)
                {
                    probability = map.TryGetValue(1, out var positiveProbability)
                        ? positiveProbability
                        : map.Values.DefaultIfEmpty(0).Max();
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
