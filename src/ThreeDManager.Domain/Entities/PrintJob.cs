namespace ThreeDManager.Domain.Entities;

public class PrintJob
{
    public Guid Id {get; set; } = Guid.NewGuid();
    public Guid? ProductId {get; set; }
    public Guid? PrinterId {get; set; }
    public Guid? MaterialId {get; set; }
    public Guid? PrintImportId {get; set; }
    public string? SourceFileName {get; set; }
    public decimal? FilamentUsedGrams {get; set; }
    public decimal? FilamentUsedMeters {get; set; }
    public int? EstimatedTimeMinutes {get; set; }
    public int? ActualTimeMinutes {get; set; }
    public decimal? ReportedCost {get; set; }
    public decimal? CalculatedMaterialCost {get; set; }
    public DateTime? StockDeductedAt {get; set; }
    public Guid? StockDeductedMaterialId {get; set; }
    public decimal? StockDeductedGrams {get; set; }
    public string Status {get; set; } = "Imported";
    public DateTime CreatedAt {get; set; } = DateTime.UtcNow;
}
