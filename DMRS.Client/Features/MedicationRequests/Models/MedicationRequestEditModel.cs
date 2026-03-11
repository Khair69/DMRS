using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.MedicationRequests.Models;

public sealed class MedicationRequestEditModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string PatientId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string MedicationText { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = "active";

    [Required]
    [MaxLength(40)]
    public string Intent { get; set; } = "order";

    public static MedicationRequestEditModel FromMedicationRequest(MedicationRequest request)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(request.Subject?.Reference, "patient") ?? string.Empty;
        var medicationConcept = request.Medication?.Concept;
        var medicationText = medicationConcept?.Text ?? medicationConcept?.Coding.FirstOrDefault()?.Code ?? string.Empty;

        return new MedicationRequestEditModel
        {
            Id = request.Id,
            PatientId = patientId,
            MedicationText = medicationText,
            Status = request.Status?.ToString().ToLowerInvariant() ?? "unknown",
            Intent = request.Intent?.ToString().ToLowerInvariant() ?? "order"
        };
    }

    public MedicationRequest ToFhirMedicationRequest()
    {
        var request = new MedicationRequest
        {
            Id = Id,
            Status = ParseStatus(Status),
            Intent = ParseIntent(Intent),
            Medication = new CodeableReference
            {
                Concept = new CodeableConcept { Text = MedicationText }
            }
        };

        var subjectRef = FhirReferenceHelper.NormalizeReference(PatientId, "Patient");
        if (!string.IsNullOrWhiteSpace(subjectRef))
        {
            request.Subject = new ResourceReference(subjectRef);
        }

        return request;
    }

    private static MedicationRequest.MedicationrequestStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MedicationRequest.MedicationrequestStatus.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "active" => MedicationRequest.MedicationrequestStatus.Active,
            "on-hold" => MedicationRequest.MedicationrequestStatus.OnHold,
            "cancelled" => MedicationRequest.MedicationrequestStatus.Cancelled,
            "completed" => MedicationRequest.MedicationrequestStatus.Completed,
            "entered-in-error" => MedicationRequest.MedicationrequestStatus.EnteredInError,
            "stopped" => MedicationRequest.MedicationrequestStatus.Stopped,
            "draft" => MedicationRequest.MedicationrequestStatus.Draft,
            _ => MedicationRequest.MedicationrequestStatus.Unknown
        };
    }

    private static MedicationRequest.MedicationRequestIntent ParseIntent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return MedicationRequest.MedicationRequestIntent.Order;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "proposal" => MedicationRequest.MedicationRequestIntent.Proposal,
            "plan" => MedicationRequest.MedicationRequestIntent.Plan,
            "order" => MedicationRequest.MedicationRequestIntent.Order,
            "original-order" => MedicationRequest.MedicationRequestIntent.OriginalOrder,
            "reflex-order" => MedicationRequest.MedicationRequestIntent.ReflexOrder,
            "filler-order" => MedicationRequest.MedicationRequestIntent.FillerOrder,
            "instance-order" => MedicationRequest.MedicationRequestIntent.InstanceOrder,
            "option" => MedicationRequest.MedicationRequestIntent.Option,
            _ => MedicationRequest.MedicationRequestIntent.Order
        };
    }
}
