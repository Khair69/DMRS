using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface ICdsHookService
    {
        Task<CdsHookResponse> EvaluateAsync(string hookId, CdsHookRequest request, CancellationToken cancellationToken);
    }
}
