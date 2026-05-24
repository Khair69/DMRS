using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface ICdsContextBuilder
    {
        Task<CdsContext> BuildAsync(CdsHookRequest request, CancellationToken cancellationToken);
    }
}
