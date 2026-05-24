using DMRS.Api.Application.ClinicalDecisionSupport.Models;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface ICardTemplateRenderer
    {
        CdsCard Render(string cardTemplateJson, CdsContext context);
    }
}
