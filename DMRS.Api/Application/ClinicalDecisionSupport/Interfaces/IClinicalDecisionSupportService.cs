using DMRS.Api.Application.ClinicalDecisionSupport.Models;
using Hl7.Fhir.Model;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IClinicalDecisionSupportService
    {
        Task<CdsEvaluationResult?> EvaluateMedicationRequestAsync(MedicationRequest request, CancellationToken cancellationToken = default);
    }
}
