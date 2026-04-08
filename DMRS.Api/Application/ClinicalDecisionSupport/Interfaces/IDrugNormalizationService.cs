using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IDrugNormalizationService
    {
        Task<IReadOnlyList<string>> NormalizeAsync(DrugConcept concept, CancellationToken cancellationToken = default);
    }
}
