using System.Globalization;
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
        var materialResult = await TryApplyStockDeductionAsync(
            printJob,
            printJob.MaterialId,
            printJob.FilamentUsedGrams,
            printJob.Status);

        if (!materialResult.Succeeded)
        {
            return materialResult;
        }

        return await TryApplyProductStockCreditAsync(
            printJob,
            printJob.ProductId,
            printJob.UnitsProduced,
            printJob.Status);
    }

    public async Task<PrintJobStockResult> SyncForEditedPrintJobAsync(
        PrintJob existingPrintJob,
        Guid? newMaterialId,
        decimal? newFilamentUsedGrams,
        string newStatus,
        Guid? newProductId,
        int newUnitsProduced)
    {
        await RestoreStockDeductionAsync(existingPrintJob);
        await RestoreProductStockCreditAsync(existingPrintJob);

        var materialResult = await TryApplyStockDeductionAsync(
            existingPrintJob,
            newMaterialId,
            newFilamentUsedGrams,
            newStatus);

        if (!materialResult.Succeeded)
        {
            return materialResult;
        }

        return await TryApplyProductStockCreditAsync(
            existingPrintJob,
            newProductId,
            newUnitsProduced,
            newStatus);
    }

    public async Task RestoreForDeletedPrintJobAsync(PrintJob printJob)
    {
        await RestoreStockDeductionAsync(printJob);
        await RestoreProductStockCreditAsync(printJob);
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
                Notes = $"Devolução automática de estoque da produção {printJob.SourceFileName} ({quantity.ToString("N2", CultureInfo.InvariantCulture)} g).",
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
        if (!string.Equals(status, PrintJobStatus.Completed, StringComparison.OrdinalIgnoreCase))
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
            Notes = $"Baixa automática de estoque pela produção {printJob.SourceFileName} ({quantity.ToString("N2", CultureInfo.InvariantCulture)} g).",
            CreatedAt = DateTime.UtcNow
        });

        printJob.StockDeductedAt = DateTime.UtcNow;
        printJob.StockDeductedMaterialId = material.Id;
        printJob.StockDeductedGrams = quantity;

        return PrintJobStockResult.Success;
    }

    private async Task RestoreProductStockCreditAsync(PrintJob printJob)
    {
        if (printJob.StockCreditedProductId is null || printJob.StockCreditedUnits is null)
        {
            return;
        }

        var product = await _context.Products.FindAsync(printJob.StockCreditedProductId.Value);

        if (product is not null)
        {
            product.StockQuantity ??= 0;

            var stockBefore = product.StockQuantity.Value;
            var quantity = printJob.StockCreditedUnits.Value;
            var stockAfter = stockBefore - quantity;

            product.StockQuantity = stockAfter;

            _context.ProductStockMovements.Add(new ProductStockMovement
            {
                ProductId = product.Id,
                PrintJobId = printJob.Id,
                MovementType = "PrintJobStockCreditReverted",
                QuantityUnits = -quantity,
                StockBeforeUnits = stockBefore,
                StockAfterUnits = stockAfter,
                Notes = $"Reversão automática de estoque da produção {printJob.SourceFileName} ({quantity} un.).",
                CreatedAt = DateTime.UtcNow
            });
        }

        printJob.StockCreditedAt = null;
        printJob.StockCreditedProductId = null;
        printJob.StockCreditedUnits = null;
    }

    private async Task<PrintJobStockResult> TryApplyProductStockCreditAsync(
        PrintJob printJob,
        Guid? productId,
        int unitsProduced,
        string status)
    {
        if (!string.Equals(status, PrintJobStatus.Completed, StringComparison.OrdinalIgnoreCase))
        {
            return PrintJobStockResult.Success;
        }

        if (productId is null)
        {
            return PrintJobStockResult.Success;
        }

        if (unitsProduced <= 0)
        {
            return PrintJobStockResult.Failure(
                nameof(PrintJob.UnitsProduced),
                "Informe as unidades produzidas para concluir e creditar estoque do produto.");
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(product => product.Id == productId.Value);

        if (product is null)
        {
            return PrintJobStockResult.Failure(
                nameof(PrintJob.ProductId),
                "Produto não encontrado para creditar estoque.");
        }

        product.StockQuantity ??= 0;

        var stockBefore = product.StockQuantity.Value;
        var stockAfter = stockBefore + unitsProduced;

        product.StockQuantity = stockAfter;
        _context.ProductStockMovements.Add(new ProductStockMovement
        {
            ProductId = product.Id,
            PrintJobId = printJob.Id,
            MovementType = "PrintJobCompleted",
            QuantityUnits = unitsProduced,
            StockBeforeUnits = stockBefore,
            StockAfterUnits = stockAfter,
            Notes = $"Crédito automático de estoque pela produção {printJob.SourceFileName} ({unitsProduced} un.).",
            CreatedAt = DateTime.UtcNow
        });

        printJob.StockCreditedAt = DateTime.UtcNow;
        printJob.StockCreditedProductId = product.Id;
        printJob.StockCreditedUnits = unitsProduced;

        return PrintJobStockResult.Success;
    }
}
