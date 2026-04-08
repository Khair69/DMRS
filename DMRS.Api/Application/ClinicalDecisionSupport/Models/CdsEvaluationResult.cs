using Hl7.Fhir.Model;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed class CdsEvaluationResult
    {
        public CdsEvaluationResult(IReadOnlyList<CdsAlert> alerts, OperationOutcome outcome)
        {
            Alerts = alerts;
            Outcome = outcome;
            HasErrors = alerts.Any(a => a.Severity == OperationOutcome.IssueSeverity.Error || a.Severity == OperationOutcome.IssueSeverity.Fatal);
            HasWarnings = alerts.Any(a => a.Severity == OperationOutcome.IssueSeverity.Warning);
        }

        public IReadOnlyList<CdsAlert> Alerts { get; }
        public OperationOutcome Outcome { get; }
        public bool HasErrors { get; }
        public bool HasWarnings { get; }
    }
}
