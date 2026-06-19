using System.ComponentModel.DataAnnotations;

namespace ThreeDManager.Web.ViewModels;

public class MaterialStockAdjustmentViewModel
{
    public Guid MaterialId { get; set; }

    public string MaterialName { get; set; } = string.Empty;

    public decimal? CurrentStockGrams { get; set; }

    [Required(ErrorMessage = "Selecione o tipo de ajuste.")]
    public string AdjustmentType { get; set; } = "Add";

    [Required(ErrorMessage = "Informe a quantidade em gramas.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public decimal QuantityGrams { get; set; }

    public string? Notes { get; set; }
}
