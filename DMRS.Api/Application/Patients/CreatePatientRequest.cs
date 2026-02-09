namespace DMRS.Api.Application.Patients
{
    public record CreatePatientRequest
    (
        string NationalId,
        string FullName,
        DateOnly BirthDate,
        string Gender
    );
}
