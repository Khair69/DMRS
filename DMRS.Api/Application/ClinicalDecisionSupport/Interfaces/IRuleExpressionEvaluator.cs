using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IRuleExpressionEvaluator
    {
        bool Evaluate(string expressionJson, CdsContext context);
    }
}
