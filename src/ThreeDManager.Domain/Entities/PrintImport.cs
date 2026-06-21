namespace ThreeDManager.Domain.Entities;

public class PrintImport
{
    public Guid Id {get; set; } = Guid.NewGuid();
    public string FileName {get; set; } = string.Empty;
    public string? FileType {get; set; }
    public string? RawContent {get; set; }
    public string? ParsedDataJson {get; set; }
    public string Status {get; set; } = PrintImportStatus.Uploaded;
    public string? ErrorMessage {get; set; }
    public DateTime ImportedAt {get; set; } = DateTime.UtcNow;
}
