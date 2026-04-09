using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IClinicalKnowledgeService
    {
        Task<IReadOnlyList<string>> GetMedicationIngredientsAsync(string medicationCode, CancellationToken cancellationToken);
        Task<MaxDoseResult?> GetMaxDoseAsync(string medicationCode, CancellationToken cancellationToken);
        Task<bool> HasAllergyContraindicationAsync(
            string medicationCode,
            IReadOnlyList<string> allergyCodes,
            CancellationToken cancellationToken);
    }
}
