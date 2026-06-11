namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class AiRiskPredictorOptions
    {
        public string ModelPath { get; set; } = "Ai/readmission_predictor.onnx";
        public string InputName { get; set; } = "float_input";
        public float HighRiskThreshold { get; set; } = 0.65f;
        public float MediumRiskThreshold { get; set; } = 0.35f;
    }
}
