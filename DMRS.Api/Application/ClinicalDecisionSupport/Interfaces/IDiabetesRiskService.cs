using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IDiabetesRiskService
    {
        Task<DiabetesRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken);
    }
}
