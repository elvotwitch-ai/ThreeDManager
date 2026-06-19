namespace ThreeDManager.Domain.Entities;

public class Material
{
    public Guid Id {get; set; } = Guid.NewGuid();
    public string Name {get; set; } = string.Empty;
    public string? Type {get; set; }
    public string? Brand {get; set; }
    public string? Color {get; set; }
    public decimal? CostPerKg {get; set; }
    public decimal? CurrentStockGrams {get; set; }
    public decimal? MinimumStockGrams {get; set; }
    public DateTime CreatedAt {get; set; } = DateTime.UtcNow;
}
