namespace ThreeDManager.Web.Presentation;

/// <summary>
/// Centralizes the per-unit production economics shown on the print-job list and
/// details surfaces. Margin and suggested sale price compare against the product's
/// <em>per-unit</em> sale price / target margin, so the cost basis must also be
/// per-unit — otherwise multi-unit (batch) jobs understate their margin by comparing
/// a per-unit sale price against a whole-batch cost. For single-unit jobs the per-unit
/// cost equals the whole-batch cost, so these helpers preserve the prior behavior.
/// </summary>
public static class PrintJobCostPresentation
{
    /// <summary>
    /// The estimated production cost divided across the units the job produces.
    /// Returns <c>null</c> when the total cost is unknown or the unit count is not positive.
    /// </summary>
    public static decimal? EstimatedCostPerUnit(decimal? totalProductionCost, int unitsProduced)
        => (totalProductionCost.HasValue && unitsProduced > 0)
            ? Math.Round(totalProductionCost.Value / unitsProduced, 2)
            : (decimal?)null;

    /// <summary>
    /// Estimated per-unit margin: the product's per-unit sale price minus the per-unit cost.
    /// </summary>
    public static decimal? EstimatedMargin(decimal? salePrice, decimal? costPerUnit)
        => (salePrice.HasValue && costPerUnit.HasValue)
            ? salePrice.Value - costPerUnit.Value
            : (decimal?)null;

    /// <summary>
    /// The margin expressed as a percentage of the sale price. Returns <c>null</c> when
    /// there is no margin to report or the sale price is not positive.
    /// </summary>
    public static decimal? EstimatedMarginPercentage(decimal? margin, decimal? salePrice)
        => (margin.HasValue && salePrice > 0m)
            ? Math.Round((margin.Value / salePrice.Value) * 100m, 1)
            : (decimal?)null;

    /// <summary>
    /// The per-unit sale price that would achieve the target margin percentage over the
    /// per-unit cost, so it is directly comparable to the product's per-unit sale price.
    /// </summary>
    public static decimal? SuggestedSalePrice(decimal? costPerUnit, decimal? targetMarginPercentage)
        => (costPerUnit.HasValue && targetMarginPercentage.HasValue)
            ? Math.Round(costPerUnit.Value * (1 + (targetMarginPercentage.Value / 100m)), 2)
            : (decimal?)null;
}
