namespace ThreeDManager.Web.Presentation;

/// <summary>
/// Centralizes the product-level costing figures derived from already-persisted
/// fields (<see cref="ThreeDManager.Domain.Entities.Product.SalePrice"/> and the
/// finished-goods stock in units). These are pure read-only projections — no schema
/// change — used to surface how much the finished-goods stock is worth at its sale
/// price. Mirrors <see cref="MaterialCostPresentation"/> for raw materials.
/// </summary>
public static class ProductCostPresentation
{
    /// <summary>
    /// The current value of the finished-goods stock at sale price:
    /// <paramref name="salePrice"/> times the <paramref name="stockQuantity"/> units
    /// on hand. Returns <c>null</c> when either the sale price or the stock quantity
    /// is unknown.
    /// </summary>
    public static decimal? StockValueAtSalePrice(decimal? salePrice, int? stockQuantity)
        => (salePrice.HasValue && stockQuantity.HasValue)
            ? Math.Round(salePrice.Value * stockQuantity.Value, 2)
            : (decimal?)null;
}
