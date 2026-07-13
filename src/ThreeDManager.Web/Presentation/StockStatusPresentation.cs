namespace ThreeDManager.Web.Presentation;

/// <summary>
/// Centralizes the low-stock badge presentation (label + Bootstrap badge class) that was
/// previously duplicated inline across the Materials and Products Index/Details views.
/// A stock item is "low stock" only when both a current stock and a minimum threshold are
/// known and the current value is at or below the minimum (the same rule the Dashboard and
/// the Index low-stock filter use). "Out of stock" is a distinct, more severe refinement of
/// the low-stock state: an alert-configured item whose current stock has run to zero (or
/// below). Accepts nullable decimals so both material grams (<see cref="decimal"/>) and
/// product units (<see cref="int"/>, implicitly widened) share one implementation.
/// </summary>
public static class StockStatusPresentation
{
    public const string OutOfStockLabel = "Sem estoque";
    public const string LowStockLabel = "Baixo estoque";
    public const string OkLabel = "Ok";
    public const string NoAlertLabel = "Sem alerta";

    public const string OutOfStockBadgeClass = "badge bg-dark";
    public const string LowStockBadgeClass = "badge bg-danger";
    public const string OkBadgeClass = "badge bg-success";
    public const string NoAlertBadgeClass = "badge bg-secondary";

    public static bool HasAlertConfigured(decimal? currentStock, decimal? minimumStock)
        => currentStock.HasValue && minimumStock.HasValue;

    public static bool IsLowStock(decimal? currentStock, decimal? minimumStock)
        => HasAlertConfigured(currentStock, minimumStock)
            && currentStock!.Value <= minimumStock!.Value;

    /// <summary>
    /// True when an alert-configured item has run out of stock (current at or below zero).
    /// This is a strict subset of <see cref="IsLowStock"/>, so low-stock filter and Dashboard
    /// counts still include out-of-stock items; only the badge presentation is refined.
    /// </summary>
    public static bool IsOutOfStock(decimal? currentStock, decimal? minimumStock)
        => IsLowStock(currentStock, minimumStock) && currentStock!.Value <= 0m;

    public static string GetLabel(decimal? currentStock, decimal? minimumStock)
    {
        if (IsOutOfStock(currentStock, minimumStock))
        {
            return OutOfStockLabel;
        }

        if (IsLowStock(currentStock, minimumStock))
        {
            return LowStockLabel;
        }

        return HasAlertConfigured(currentStock, minimumStock) ? OkLabel : NoAlertLabel;
    }

    public static string GetBadgeClass(decimal? currentStock, decimal? minimumStock)
    {
        if (IsOutOfStock(currentStock, minimumStock))
        {
            return OutOfStockBadgeClass;
        }

        if (IsLowStock(currentStock, minimumStock))
        {
            return LowStockBadgeClass;
        }

        return HasAlertConfigured(currentStock, minimumStock) ? OkBadgeClass : NoAlertBadgeClass;
    }
}
