namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class DiabetesRiskPredictorOptions
    {
        public string ModelPath { get; set; } = "Ai/diabetes_predictor.onnx";
        public string InputName { get; set; } = "float_input";
        public float HighRiskThreshold { get; set; } = 0.5f;
    }
}
