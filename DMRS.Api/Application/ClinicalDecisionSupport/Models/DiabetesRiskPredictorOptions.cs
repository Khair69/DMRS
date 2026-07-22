namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class DiabetesRiskPredictorOptions
    {
        public string ModelPath { get; set; } = "Ai/diabetes_predictor.onnx";
        public string InputName { get; set; } = "float_input";

        // Both the High/Medium banding and the IsHighRisk flag derive from these, so the flag can
        // never disagree with the reported level. Values match the readmission predictor's, keeping
        // the three models' risk bands consistent.
        public float HighRiskThreshold { get; set; } = 0.65f;
        public float MediumRiskThreshold { get; set; } = 0.35f;
    }
}
