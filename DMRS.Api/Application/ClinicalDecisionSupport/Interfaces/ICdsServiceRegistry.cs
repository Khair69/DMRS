using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface ICdsServiceRegistry
    {
        IReadOnlyList<CdsServiceDefinition> ListServices();
        CdsServiceDefinition? GetService(string id);
    }
}
