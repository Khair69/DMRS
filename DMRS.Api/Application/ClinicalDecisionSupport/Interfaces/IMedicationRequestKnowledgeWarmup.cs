using Hl7.Fhir.Model;

namespace DMRS.Api.Application.ClinicalDecisionSupport.Interfaces
{
    public interface IMedicationRequestKnowledgeWarmup
    {
        System.Threading.Tasks.Task WarmAsync(MedicationRequest medicationRequest, CancellationToken cancellationToken);
    }
}
