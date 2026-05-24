namespace DMRS.Api.Application.Documents
{
    public sealed class PatientDocumentRecord
    {
        public string Id { get; set; } = string.Empty;
        public string PatientId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
    }
}
