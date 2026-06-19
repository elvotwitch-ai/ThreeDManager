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
            material.CurrentStockGrams ??= 0;

            var stockBefore = material.CurrentStockGrams.Value;
            var quantity = printJob.StockDeductedGrams.Value;
            var stockAfter = stockBefore + quantity;

            material.CurrentStockGrams = stockAfter;

            _context.MaterialStockMovements.Add(new MaterialStockMovement
            {
                MaterialId = material.Id,
                PrintJobId = printJob.Id,
                MovementType = "PrintJobStockRestored",
                QuantityGrams = quantity,
                StockBeforeGrams = stockBefore,
                StockAfterGrams = stockAfter,
                Notes = $"Devolução automática da produção {printJob.SourceFileName}",
                CreatedAt = DateTime.UtcNow
            });
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

        var stockBefore = material.CurrentStockGrams.Value;
        var quantity = filamentUsedGrams.Value;
        var stockAfter = stockBefore - quantity;

        material.CurrentStockGrams = stockAfter;
        _context.MaterialStockMovements.Add(new MaterialStockMovement
        {
            MaterialId = material.Id,
            PrintJobId = printJob.Id,
            MovementType = "PrintJobCompleted",
            QuantityGrams = -quantity,
            StockBeforeGrams = stockBefore,
            StockAfterGrams = stockAfter,
            Notes = $"Baixa automática pela produção {printJob.SourceFileName}",
            CreatedAt = DateTime.UtcNow
        });

        printJob.StockDeductedAt = DateTime.UtcNow;
        printJob.StockDeductedMaterialId = material.Id;
        printJob.StockDeductedGrams = quantity;

        return PrintJobStockResult.Success;
    }
}
