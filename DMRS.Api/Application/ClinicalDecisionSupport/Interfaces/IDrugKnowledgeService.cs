using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IDrugKnowledgeService
    {
        Task<IReadOnlyList<DrugKnowledge>> FindByCodesAsync(IEnumerable<string> ingredientRxCuis, CancellationToken cancellationToken = default);
    }
}
