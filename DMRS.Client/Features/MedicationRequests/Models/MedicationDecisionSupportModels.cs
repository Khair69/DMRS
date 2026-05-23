using DMRS.Client.Features.Cds.Models;

namespace DMRS.Client.Features.MedicationRequests.Models;

public sealed class MedicationDecisionSupportSnapshotModel
{
    public CdsMedicineKnowledgeModel? Medicine { get; set; }
    public CdsHookResponseModel? HookResponse { get; set; }
}
