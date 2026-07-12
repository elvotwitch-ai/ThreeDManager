namespace ThreeDManager.Domain.Entities;

public static class StockMovementType
{
    public const string ManualAdjustment = "ManualAdjustment";
    public const string PrintJobCompleted = "PrintJobCompleted";
    public const string PrintJobStockRestored = "PrintJobStockRestored";
    public const string PrintJobStockCreditReverted = "PrintJobStockCreditReverted";
}
