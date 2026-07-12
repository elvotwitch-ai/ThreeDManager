using ThreeDManager.Domain.Entities;

namespace ThreeDManager.Web.Presentation;

public static class StockMovementTypePresentation
{
    public static string? GetMaterialMovementLabel(string? movementType)
    {
        return movementType switch
        {
            StockMovementType.PrintJobCompleted => "Baixa automática",
            StockMovementType.PrintJobStockRestored => "Devolução automática",
            StockMovementType.ManualAdjustment => "Ajuste manual",
            _ => movementType
        };
    }

    public static string? GetProductMovementLabel(string? movementType)
    {
        return movementType switch
        {
            StockMovementType.PrintJobCompleted => "Crédito automático",
            StockMovementType.PrintJobStockCreditReverted => "Reversão automática",
            StockMovementType.ManualAdjustment => "Ajuste manual",
            _ => movementType
        };
    }
}
