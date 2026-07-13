using System.ComponentModel.DataAnnotations;

namespace ThreeDManager.Web.ViewModels;

public class ProductStockAdjustmentViewModel : IValidatableObject
{
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public int? CurrentStockQuantity { get; set; }

    [Required(ErrorMessage = "Selecione o tipo de ajuste.")]
    public string AdjustmentType { get; set; } = StockAdjustmentType.Add;

    public int QuantityUnits { get; set; }

    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // "Definir estoque exato" (Set) accepts 0 as a valid target — e.g. "acabou o
        // estoque, corrigir para 0". Add/Remove operate on a delta, so they still require
        // a strictly positive quantity. Negatives are never a valid quantity.
        if (AdjustmentType == StockAdjustmentType.Set)
        {
            if (QuantityUnits < 0)
            {
                yield return new ValidationResult(
                    "A quantidade não pode ser negativa.",
                    new[] { nameof(QuantityUnits) });
            }
        }
        else if (QuantityUnits < 1)
        {
            yield return new ValidationResult(
                "A quantidade deve ser maior que zero.",
                new[] { nameof(QuantityUnits) });
        }
    }
}
