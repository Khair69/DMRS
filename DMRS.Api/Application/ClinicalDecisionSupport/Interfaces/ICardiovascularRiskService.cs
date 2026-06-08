using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface ICardiovascularRiskService
    {
        Task<CardiovascularRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken);
    }
}
