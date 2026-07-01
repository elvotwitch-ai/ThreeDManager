using System.ComponentModel.DataAnnotations;

namespace ThreeDManager.Domain.Entities;

public class PrintJob
{
    public Guid Id {get; set; } = Guid.NewGuid();
    public Guid? ProductId {get; set; }
    public Guid? PrinterId {get; set; }
    public Guid? MaterialId {get; set; }
    public Guid? PrintImportId {get; set; }
    public string? SourceFileName {get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O filamento usado (g) não pode ser negativo.")]
    public decimal? FilamentUsedGrams {get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O filamento usado (m) não pode ser negativo.")]
    public decimal? FilamentUsedMeters {get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O tempo estimado não pode ser negativo.")]
    public int? EstimatedTimeMinutes {get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O tempo real não pode ser negativo.")]
    public int? ActualTimeMinutes {get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O custo informado não pode ser negativo.")]
    public decimal? ReportedCost {get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "O custo de embalagem não pode ser negativo.")]
    public decimal? PackagingCost {get; set; }
    public decimal? CalculatedMaterialCost {get; set; }
    public DateTime? StockDeductedAt {get; set; }
    public Guid? StockDeductedMaterialId {get; set; }
    public decimal? StockDeductedGrams {get; set; }
    public string Status {get; set; } = PrintJobStatus.Imported;
    public DateTime CreatedAt {get; set; } = DateTime.UtcNow;
}
