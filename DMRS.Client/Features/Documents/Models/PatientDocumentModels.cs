namespace DMRS.Client.Features.Documents.Models;

public sealed class PatientDocumentModel
{
    public string Id { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;

    public string SizeDisplay => SizeBytes switch
    {
        >= 1_048_576 => $"{SizeBytes / 1_048_576.0:0.#} MB",
        >= 1_024 => $"{SizeBytes / 1_024.0:0.#} KB",
        _ => $"{SizeBytes} B"
    };

    public string FileIcon => ContentType switch
    {
        "application/pdf" => "📄",
        var ct when ct.StartsWith("image/") => "🖼️",
        var ct when ct.Contains("word") => "📝",
        var ct when ct.Contains("sheet") || ct.Contains("excel") => "📊",
        _ => "📎"
    };
}
