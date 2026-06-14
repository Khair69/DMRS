using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IHighUtilizationRiskService
    {
        Task<HighUtilizationRiskAssessment?> AssessPatientAsync(string patientId, CancellationToken cancellationToken);

        /// <summary>
        /// Scores every eligible patient (one with a birth date and a male/female gender) in a single
        /// pass, so the dashboard makes one request instead of one per patient. When
        /// <paramref name="patientIdFilter"/> is non-null, only patients in that set are scored
        /// (used to scope aggregate views to a caller's organization); null scores all patients.
        /// </summary>
        Task<IReadOnlyList<HighUtilizationRiskAssessment>> AssessAllAsync(IReadOnlyCollection<string>? patientIdFilter, CancellationToken cancellationToken);
    }
}
