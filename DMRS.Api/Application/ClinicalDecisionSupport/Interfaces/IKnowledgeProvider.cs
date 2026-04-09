using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IKnowledgeProvider
    {
        string SourceName { get; }
        Task<IReadOnlyList<string>> GetMedicationIngredientsAsync(string medicationCode, CancellationToken cancellationToken);
        Task<MaxDoseResult?> GetMaxDoseAsync(string medicationCode, CancellationToken cancellationToken);
    }
}
