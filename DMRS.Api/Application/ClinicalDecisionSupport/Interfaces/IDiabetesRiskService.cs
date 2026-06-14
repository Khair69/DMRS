using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IDiabetesRiskService
    {
        Task<DiabetesRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken);

        /// <summary>
        /// Scores every patient with a birth date in a single pass (one cohort-wide Observation load),
        /// so the AI Insights page makes one request instead of one per patient.
        /// </summary>
        Task<IReadOnlyList<DiabetesRiskAssessment>> AssessAllAsync(CancellationToken cancellationToken);
    }
}
