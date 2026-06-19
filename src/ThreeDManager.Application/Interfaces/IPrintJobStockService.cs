using ThreeDManager.Domain.Entities;

namespace ThreeDManager.Application.Interfaces;

public interface IPrintJobStockService
{
    Task<PrintJobStockResult> ApplyForNewPrintJobAsync(PrintJob printJob);

    Task<PrintJobStockResult> SyncForEditedPrintJobAsync(
        PrintJob existingPrintJob,
        Guid? newMaterialId,
        decimal? newFilamentUsedGrams,
        string newStatus);

    Task RestoreForDeletedPrintJobAsync(PrintJob printJob);
}

public sealed record PrintJobStockResult(
    bool Succeeded,
    string? FieldName = null,
    string? ErrorMessage = null)
{
    public static PrintJobStockResult Success { get; } = new(true);

    public static PrintJobStockResult Failure(string fieldName, string errorMessage)
    {
        return new PrintJobStockResult(false, fieldName, errorMessage);
    }
}
