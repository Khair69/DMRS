using Hl7.Fhir.Model;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Models
{
    public sealed record CdsAlert(string Code, string Message, OperationOutcome.IssueSeverity Severity);
}
