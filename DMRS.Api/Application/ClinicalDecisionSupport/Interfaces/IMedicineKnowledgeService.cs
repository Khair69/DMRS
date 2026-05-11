using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IMedicineKnowledgeService
    {
        Task<MedicineKnowledge?> GetAsync(string medicationCode, CancellationToken cancellationToken);
        Task<MedicineKnowledge?> RefreshAsync(string medicationCode, CancellationToken cancellationToken);
        Task<IReadOnlyList<MedicineKnowledge>> SearchAsync(
            string? query,
            string? ingredient,
            string? indication,
            int limit,
            CancellationToken cancellationToken);
    }
}
