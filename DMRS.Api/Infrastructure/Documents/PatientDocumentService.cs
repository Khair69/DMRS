using System.Text.Json;
using DMRS.Api.Application.Documents;

namespace DMRS.Api.Infrastructure.Documents
{
    public sealed class PatientDocumentService : IPatientDocumentService
    {
        private readonly string _rootPath;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public PatientDocumentService(IWebHostEnvironment environment)
        {
            _rootPath = Path.Combine(environment.ContentRootPath, "Documents");
            Directory.CreateDirectory(_rootPath);
        }

        public async Task<IReadOnlyList<PatientDocumentRecord>> ListAsync(string patientId)
        {
            var metaPath = GetMetaPath(patientId);
            if (!File.Exists(metaPath))
            {
                return [];
            }

            await using var stream = File.OpenRead(metaPath);
            var records = await JsonSerializer.DeserializeAsync<List<PatientDocumentRecord>>(stream, JsonOptions);
            return (records ?? [])
                .OrderByDescending(r => r.UploadedAt)
                .ToList();
        }

        public async Task<PatientDocumentRecord> SaveAsync(
            string patientId,
            string fileName,
            string contentType,
            Stream content,
            string uploadedBy)
        {
            var patientDir = GetPatientDir(patientId);
            Directory.CreateDirectory(patientDir);

            var documentId = Guid.NewGuid().ToString("N");
            var safeFileName = Path.GetFileName(fileName);
            var storedFileName = $"{documentId}_{safeFileName}";
            var filePath = Path.Combine(patientDir, storedFileName);

            await using (var dest = File.Create(filePath))
            {
                await content.CopyToAsync(dest);
            }

            var sizeBytes = new FileInfo(filePath).Length;

            var record = new PatientDocumentRecord
            {
                Id = documentId,
                PatientId = patientId,
                FileName = safeFileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                UploadedAt = DateTimeOffset.UtcNow,
                UploadedBy = uploadedBy,
                StoredFileName = storedFileName
            };

            var existing = (await ListAsync(patientId)).ToList();
            existing.Insert(0, record);
            await SaveMetaAsync(patientId, existing);

            return record;
        }

        public async Task<(PatientDocumentRecord record, Stream content)?> GetContentAsync(string patientId, string documentId)
        {
            var records = await ListAsync(patientId);
            var record = records.FirstOrDefault(r => r.Id == documentId);
            if (record is null)
            {
                return null;
            }

            var filePath = Path.Combine(GetPatientDir(patientId), record.StoredFileName);
            if (!File.Exists(filePath))
            {
                return null;
            }

            return (record, File.OpenRead(filePath));
        }

        public async Task<bool> DeleteAsync(string patientId, string documentId)
        {
            var records = (await ListAsync(patientId)).ToList();
            var record = records.FirstOrDefault(r => r.Id == documentId);
            if (record is null)
            {
                return false;
            }

            var filePath = Path.Combine(GetPatientDir(patientId), record.StoredFileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            records.Remove(record);
            await SaveMetaAsync(patientId, records);
            return true;
        }

        private async System.Threading.Tasks.Task SaveMetaAsync(string patientId, IEnumerable<PatientDocumentRecord> records)
        {
            Directory.CreateDirectory(GetPatientDir(patientId));
            var metaPath = GetMetaPath(patientId);
            await using var stream = File.Create(metaPath);
            await JsonSerializer.SerializeAsync(stream, records.ToList(), JsonOptions);
        }

        private string GetPatientDir(string patientId)
            => Path.Combine(_rootPath, SanitizeId(patientId));

        private string GetMetaPath(string patientId)
            => Path.Combine(GetPatientDir(patientId), "metadata.json");

        private static string SanitizeId(string id)
            => string.Concat(id.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
    }
}
