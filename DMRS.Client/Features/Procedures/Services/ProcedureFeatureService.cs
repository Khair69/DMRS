using DMRS.Client.Features.Procedures.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Procedures.Services;

public sealed class ProcedureFeatureService : FhirFeatureServiceBase<Procedure, ProcedureEditModel, ProcedureSummaryViewModel>
{
    public ProcedureFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override Procedure ToResource(ProcedureEditModel model)
        => model.ToFhirProcedure();

    protected override ProcedureSummaryViewModel MapToSummary(Procedure procedure)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(procedure.Subject?.Reference, "patient") ?? "(unknown)";
        var codeText = procedure.Code?.Text ?? procedure.Code?.Coding.FirstOrDefault()?.Code ?? "(no-code)";
        var status = procedure.Status?.ToString() ?? "unknown";

        return new ProcedureSummaryViewModel(
            procedure.Id ?? "(no-id)",
            patientId,
            codeText,
            status);
    }
}
