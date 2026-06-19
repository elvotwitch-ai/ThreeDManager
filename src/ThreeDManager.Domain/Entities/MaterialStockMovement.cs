namespace ThreeDManager.Domain.Entities;

public class MaterialStockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaterialId { get; set; }
    public Guid? PrintJobId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal QuantityGrams { get; set; }
    public decimal? StockBeforeGrams { get; set; }
    public decimal? StockAfterGrams { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
