using System.ComponentModel.DataAnnotations;

namespace ThreeDManager.Web.ViewModels;

public class ProductStockAdjustmentViewModel
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int? CurrentStockQuantity { get; set; }

    [Required(ErrorMessage = "Selecione o tipo de ajuste.")]
    public string AdjustmentType { get; set; } = StockAdjustmentType.Add;

    [Required(ErrorMessage = "Informe a quantidade em unidades.")]
    [Range(1, int.MaxValue, ErrorMessage = "A quantidade deve ser maior que zero.")]
    public int QuantityUnits { get; set; }

    public string? Notes { get; set; }
}
