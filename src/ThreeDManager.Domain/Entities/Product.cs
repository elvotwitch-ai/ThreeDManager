using System.ComponentModel.DataAnnotations;

namespace ThreeDManager.Domain.Entities;

public class Product
{
    public Guid Id {get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Informe o nome do produto.")]
    public string Name {get; set; } = string.Empty;

    public string? Sku {get; set; }
    public string? Category {get; set; }
    public string? Description {get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O preço de venda não pode ser negativo.")]
    public decimal? SalePrice {get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O estoque de produtos acabados não pode ser negativo.")]
    public int? StockQuantity {get; set; }

    public bool IsActive {get; set; } = true;
    public DateTime CreatedAt {get; set; } = DateTime.UtcNow;
    }