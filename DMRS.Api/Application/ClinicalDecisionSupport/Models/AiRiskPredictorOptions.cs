namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class AiRiskPredictorOptions
    {
        public string ModelPath { get; set; } = "Ai/high_risk_predictor.onnx";
        public string InputName { get; set; } = "float_input";
        public float HighRiskThreshold { get; set; } = 0.5f;
    }
}
