using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface ICdsVariableCatalog
    {
        IReadOnlyList<CdsVariableDefinition> ListVariables();
    }
}
