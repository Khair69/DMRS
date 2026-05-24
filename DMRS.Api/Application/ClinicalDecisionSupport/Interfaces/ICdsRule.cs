using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface ICdsRule
    {
        Task<IReadOnlyList<CdsCard>> EvaluateAsync(CdsContext context, CancellationToken cancellationToken);
    }
}
