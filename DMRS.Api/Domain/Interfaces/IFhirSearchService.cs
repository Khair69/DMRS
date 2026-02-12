using Hl7.Fhir.Model;

namespace DMRS.Api.Domain.Interfaces
{
    public interface IFhirSearchService
    {
        Task<List<T>> SearchAsync<T>(IEnumerable<Tuple<string, string>> searchParams) where T : Resource;
    }
}
