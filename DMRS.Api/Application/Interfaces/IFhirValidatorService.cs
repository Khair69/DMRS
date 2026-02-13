using Hl7.Fhir.Model;

namespace DMRS.Api.Application.Interfaces
{
    public interface IFhirValidatorService
    {
        Task<OperationOutcome> ValidateAsync(Resource resource, string? profileUrl = null);
    }
}
