namespace ThreeDManager.Domain.Entities;

public class ProductStockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid? PrintJobId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int QuantityUnits { get; set; }
    public int? StockBeforeUnits { get; set; }
    public int? StockAfterUnits { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
