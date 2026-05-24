namespace ThreeDManager.Domain.Entities;

public class Product
{
    public Guid Id {get; set; } = Guid.NewGuid();
    public string Name {get; set; } = string.Empty;
    public string? Sku {get; set; } 
    public string? Category {get; set; }
    public string? Description {get; set; }
    public decimal? SalePrice {get; set; }
    public bool IsActive {get; set; } = true;
    public DateTime CreatedAt {get; set; } = DateTime.UtcNow;
    }