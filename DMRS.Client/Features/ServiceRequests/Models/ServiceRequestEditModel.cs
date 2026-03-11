using System.ComponentModel.DataAnnotations;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.ServiceRequests.Models;

public sealed class ServiceRequestEditModel
{
    public string? Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string PatientId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string CodeText { get; set; } = string.Empty;

    [Required]
    [MaxLength(40)]
    public string Status { get; set; } = "active";

    [Required]
    [MaxLength(40)]
    public string Intent { get; set; } = "order";

    public DateTime? OccurrenceDateTime { get; set; }

    public static ServiceRequestEditModel FromServiceRequest(ServiceRequest request)
    {
        var patientId = FhirReferenceHelper.ExtractReferenceId(request.Subject?.Reference, "patient") ?? string.Empty;
        var codeConcept = request.Code?.Concept;
        var codeText = codeConcept?.Text ?? codeConcept?.Coding.FirstOrDefault()?.Code ?? string.Empty;
        var status = request.Status?.ToString().ToLowerInvariant() ?? "unknown";
        var intent = request.Intent?.ToString().ToLowerInvariant() ?? "order";

        DateTime? occurrence = null;
        if (request.Occurrence is FhirDateTime dateTime && DateTime.TryParse(dateTime.Value, out var parsed))
        {
            occurrence = parsed;
        }

        return new ServiceRequestEditModel
        {
            Id = request.Id,
            PatientId = patientId,
            CodeText = codeText,
            Status = status,
            Intent = intent,
            OccurrenceDateTime = occurrence
        };
    }

    public ServiceRequest ToFhirServiceRequest()
    {
        var request = new ServiceRequest
        {
            Id = Id,
            Status = ParseStatus(Status),
            Intent = ParseIntent(Intent),
            Code = new CodeableReference
            {
                Concept = new CodeableConcept { Text = CodeText }
            }
        };

        var subjectRef = FhirReferenceHelper.NormalizeReference(PatientId, "Patient");
        if (!string.IsNullOrWhiteSpace(subjectRef))
        {
            request.Subject = new ResourceReference(subjectRef);
        }

        if (OccurrenceDateTime.HasValue)
        {
            request.Occurrence = new FhirDateTime(OccurrenceDateTime.Value);
        }

        return request;
    }

    private static RequestStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RequestStatus.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "draft" => RequestStatus.Draft,
            "active" => RequestStatus.Active,
            "on-hold" => RequestStatus.OnHold,
            "revoked" => RequestStatus.Revoked,
            "completed" => RequestStatus.Completed,
            "entered-in-error" => RequestStatus.EnteredInError,
            _ => RequestStatus.Unknown
        };
    }

    private static RequestIntent ParseIntent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RequestIntent.Order;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "proposal" => RequestIntent.Proposal,
            "plan" => RequestIntent.Plan,
            "order" => RequestIntent.Order,
            "original-order" => RequestIntent.OriginalOrder,
            "reflex-order" => RequestIntent.ReflexOrder,
            "filler-order" => RequestIntent.FillerOrder,
            "instance-order" => RequestIntent.InstanceOrder,
            "option" => RequestIntent.Option,
            _ => RequestIntent.Order
        };
    }
}
