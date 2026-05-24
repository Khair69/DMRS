namespace DMRS.Api.Application.Documents
{
    public interface IPatientDocumentService
    {
        Task<IReadOnlyList<PatientDocumentRecord>> ListAsync(string patientId);
        Task<PatientDocumentRecord> SaveAsync(string patientId, string fileName, string contentType, Stream content, string uploadedBy);
        Task<(PatientDocumentRecord record, Stream content)?> GetContentAsync(string patientId, string documentId);
        Task<bool> DeleteAsync(string patientId, string documentId);
    }
}
