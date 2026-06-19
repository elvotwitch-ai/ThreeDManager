using Microsoft.EntityFrameworkCore;
using ThreeDManager.Application.Interfaces;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;

namespace ThreeDManager.Infrastructure.Services;

public class PrintJobStockService : IPrintJobStockService
{
    private readonly AppDbContext _context;

    public PrintJobStockService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PrintJobStockResult> ApplyForNewPrintJobAsync(PrintJob printJob)
    {
        return await TryApplyStockDeductionAsync(
            printJob,
            printJob.MaterialId,
            printJob.FilamentUsedGrams,
            printJob.Status);
    }

    public async Task<PrintJobStockResult> SyncForEditedPrintJobAsync(
        PrintJob existingPrintJob,
        Guid? newMaterialId,
        decimal? newFilamentUsedGrams,
        string newStatus)
    {
        await RestoreStockDeductionAsync(existingPrintJob);

        return await TryApplyStockDeductionAsync(
            existingPrintJob,
            newMaterialId,
            newFilamentUsedGrams,
            newStatus);
    }

    public async Task RestoreForDeletedPrintJobAsync(PrintJob printJob)
    {
        await RestoreStockDeductionAsync(printJob);
    }

    private async Task RestoreStockDeductionAsync(PrintJob printJob)
    {
        if (printJob.StockDeductedMaterialId is null || printJob.StockDeductedGrams is null)
        {
            return;
        }

        var material = await _context.Materials.FindAsync(printJob.StockDeductedMaterialId.Value);

        if (material is not null)
        {
            material.CurrentStockGrams = (material.CurrentStockGrams ?? 0) + printJob.StockDeductedGrams.Value;
        }

        printJob.StockDeductedAt = null;
        printJob.StockDeductedMaterialId = null;
        printJob.StockDeductedGrams = null;
    }

    private async Task<PrintJobStockResult> TryApplyStockDeductionAsync(
        PrintJob printJob,
        Guid? materialId,
        decimal? filamentUsedGrams,
        string status)
    {
        if (!string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return PrintJobStockResult.Success;
        }

        if (materialId is null)
        {
            return PrintJobStockResult.Failure(
                nameof(PrintJob.MaterialId),
                "Selecione um material para concluir e baixar estoque.");
        }

        if (filamentUsedGrams is null or <= 0)
        {
            return PrintJobStockResult.Failure(
                nameof(PrintJob.FilamentUsedGrams),
                "Informe o filamento usado para concluir e baixar estoque.");
        }

        var material = await _context.Materials
            .FirstOrDefaultAsync(material => material.Id == materialId.Value);

        if (material is null)
        {
            return PrintJobStockResult.Failure(
                nameof(PrintJob.MaterialId),
                "Material não encontrado para baixar estoque.");
        }

        if (material.CurrentStockGrams is null)
        {
            return PrintJobStockResult.Failure(
                nameof(PrintJob.MaterialId),
                "O material selecionado não possui estoque informado.");
        }

        if (material.CurrentStockGrams.Value < filamentUsedGrams.Value)
        {
            return PrintJobStockResult.Failure(
                nameof(PrintJob.FilamentUsedGrams),
                "Estoque insuficiente para concluir esta produção.");
        }

        material.CurrentStockGrams -= filamentUsedGrams.Value;
        printJob.StockDeductedAt = DateTime.UtcNow;
        printJob.StockDeductedMaterialId = material.Id;
        printJob.StockDeductedGrams = filamentUsedGrams.Value;

        return PrintJobStockResult.Success;
    }
}
