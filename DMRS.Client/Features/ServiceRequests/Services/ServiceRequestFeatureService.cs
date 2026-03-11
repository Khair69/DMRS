using DMRS.Client.Features.ServiceRequests.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.ServiceRequests.Services;

public sealed class ServiceRequestFeatureService : FhirFeatureServiceBase<ServiceRequest, ServiceRequestEditModel, ServiceRequestSummaryViewModel>
{
    public ServiceRequestFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override ServiceRequest ToResource(ServiceRequestEditModel model)
        => model.ToFhirServiceRequest();

    protected override ServiceRequestSummaryViewModel MapToSummary(ServiceRequest request)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(request.Subject?.Reference, "patient") ?? "(unknown)";
        var codeConcept = request.Code?.Concept;
        var codeText = codeConcept?.Text ?? codeConcept?.Coding.FirstOrDefault()?.Code ?? "(no-code)";

        return new ServiceRequestSummaryViewModel(
            request.Id ?? "(no-id)",
            patientId,
            codeText,
            request.Status?.ToString() ?? "unknown",
            request.Intent?.ToString() ?? "unknown");
    }
}
