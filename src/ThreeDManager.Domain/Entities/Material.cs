using System.ComponentModel.DataAnnotations;

namespace ThreeDManager.Domain.Entities;

public class Material
{
    public Guid Id {get; set; } = Guid.NewGuid();

    [Required(ErrorMessage = "Informe o nome do material.")]
    public string Name {get; set; } = string.Empty;

    public string? Type {get; set; }
    public string? Brand {get; set; }
    public string? Color {get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O custo por kg não pode ser negativo.")]
    public decimal? CostPerKg {get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O estoque atual não pode ser negativo.")]
    public decimal? CurrentStockGrams {get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "O estoque mínimo não pode ser negativo.")]
    public decimal? MinimumStockGrams {get; set; }

    public DateTime CreatedAt {get; set; } = DateTime.UtcNow;
}
