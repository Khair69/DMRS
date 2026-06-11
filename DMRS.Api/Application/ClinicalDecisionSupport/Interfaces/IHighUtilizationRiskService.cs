using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IHighUtilizationRiskService
    {
        Task<HighUtilizationRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken);

        /// <summary>
        /// Scores every eligible patient (one with a birth date and a male/female gender) in a single
        /// pass, so the dashboard makes one request instead of one per patient.
        /// </summary>
        Task<IReadOnlyList<HighUtilizationRiskAssessment>> AssessAllAsync(CancellationToken cancellationToken);
    }
}
