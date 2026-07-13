namespace ThreeDManager.Web.Presentation;

/// <summary>
/// Centralizes the material-level costing figures derived from already-persisted
/// fields (<see cref="ThreeDManager.Domain.Entities.Material.CostPerKg"/> and the
/// current stock in grams). These are pure read-only projections — no schema change —
/// used to surface how much money is currently tied up in a material's stock.
/// </summary>
public static class MaterialCostPresentation
{
    /// <summary>
    /// The current monetary value of the material in stock: cost per gram
    /// (<paramref name="costPerKg"/> ÷ 1000) times the grams on hand. Returns
    /// <c>null</c> when either the unit cost or the current stock is unknown.
    /// </summary>
    public static decimal? InventoryValue(decimal? costPerKg, decimal? currentStockGrams)
        => (costPerKg.HasValue && currentStockGrams.HasValue)
            ? Math.Round((costPerKg.Value / 1000m) * currentStockGrams.Value, 2)
            : (decimal?)null;
}
