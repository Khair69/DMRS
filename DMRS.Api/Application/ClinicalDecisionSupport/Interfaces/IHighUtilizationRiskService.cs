using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IHighUtilizationRiskService
    {
        Task<HighUtilizationRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken);
    }
}
