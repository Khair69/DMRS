using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IRuleEngine
    {
        Task<IReadOnlyList<CdsCard>> EvaluateAsync(
            CdsContext context,
            IEnumerable<ICdsRule> rules,
            CancellationToken cancellationToken);
    }
}
