using System.ComponentModel.DataAnnotations;

namespace ThreeDManager.Web.ViewModels;

public class MaterialStockAdjustmentViewModel : IValidatableObject
{
    public Guid MaterialId { get; set; }

    public string MaterialName { get; set; } = string.Empty;

    public decimal? CurrentStockGrams { get; set; }

    [Required(ErrorMessage = "Selecione o tipo de ajuste.")]
    public string AdjustmentType { get; set; } = StockAdjustmentType.Add;

    public decimal QuantityGrams { get; set; }

    public string? Notes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // "Definir estoque exato" (Set) accepts 0 as a valid target — e.g. "o material
        // acabou, corrigir o saldo para 0". Add/Remove operate on a delta, so they still
        // require a strictly positive quantity. Negatives are never a valid quantity.
        if (AdjustmentType == StockAdjustmentType.Set)
        {
            if (QuantityGrams < 0m)
            {
                yield return new ValidationResult(
                    "A quantidade não pode ser negativa.",
                    new[] { nameof(QuantityGrams) });
            }
        }
        else if (QuantityGrams < 0.01m)
        {
            yield return new ValidationResult(
                "A quantidade deve ser maior que zero.",
                new[] { nameof(QuantityGrams) });
        }
    }
}
