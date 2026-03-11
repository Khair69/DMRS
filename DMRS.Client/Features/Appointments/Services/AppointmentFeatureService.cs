using DMRS.Client.Features.Appointments.Models;
using DMRS.Client.Services;
using Hl7.Fhir.Model;

namespace DMRS.Client.Features.Appointments.Services;

public sealed class AppointmentFeatureService : FhirFeatureServiceBase<Appointment, AppointmentEditModel, AppointmentSummaryViewModel>
{
    public AppointmentFeatureService(FhirApiService fhirApiService) : base(fhirApiService)
    {
    }

    protected override Appointment ToResource(AppointmentEditModel model)
        => model.ToFhirAppointment();

    protected override AppointmentSummaryViewModel MapToSummary(Appointment appointment)
    {
        var patientId = appointment.Participant
            .Select(p => FhirReferenceHelper.ExtractReferenceId(p.Actor?.Reference, "patient"))
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? "(unknown)";

        var start = appointment.StartElement?.Value?.ToString("u");

        return new AppointmentSummaryViewModel(
            appointment.Id ?? "(no-id)",
            patientId,
            appointment.Status?.ToString() ?? "unknown",
            start);
    }
}
