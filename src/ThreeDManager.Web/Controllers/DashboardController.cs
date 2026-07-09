using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Application.Interfaces;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Web.ViewModels;

namespace ThreeDManager.Web.Controllers;

public class DashboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPrintFileParser _parser;

    public DashboardController(AppDbContext context, IPrintFileParser parser)
    {
        _context = context;
        _parser = parser;
    }

    public async Task<IActionResult> Index()
    {
        var printImports = await _context.PrintImports
            .OrderByDescending(printImport => printImport.ImportedAt)
            .ToListAsync();

        var printJobs = await _context.PrintJobs
            .OrderByDescending(printJob => printJob.CreatedAt)
            .ToListAsync();

        var linkedPrintImportIds = printJobs
            .Where(printJob => printJob.PrintImportId.HasValue)
            .Select(printJob => printJob.PrintImportId!.Value)
            .ToHashSet();

        var productNames = await _context.Products
            .ToDictionaryAsync(product => product.Id, product => product.Name);

        var materialNames = await _context.Materials
            .ToDictionaryAsync(material => material.Id, material => material.Name);

        var printerNames = await _context.Printers
            .ToDictionaryAsync(printer => printer.Id, printer => printer.Name);

        var printerCostPerHour = await _context.Printers
            .ToDictionaryAsync(printer => printer.Id, printer => printer.CostPerHour);

        var productSalePriceById = await _context.Products
            .ToDictionaryAsync(product => product.Id, product => product.SalePrice);

        var lowStockMaterials = await _context.Materials
            .Where(material =>
                material.CurrentStockGrams.HasValue
                && material.MinimumStockGrams.HasValue
                && material.CurrentStockGrams.Value <= material.MinimumStockGrams.Value)
            .OrderBy(material => material.CurrentStockGrams)
            .ToListAsync();

        var lowStockProducts = await _context.Products
            .Where(product =>
                product.StockQuantity.HasValue
                && product.MinimumStockQuantity.HasValue
                && product.StockQuantity.Value <= product.MinimumStockQuantity.Value)
            .OrderBy(product => product.StockQuantity)
            .ToListAsync();

        var stockMovements = await _context.MaterialStockMovements
            .OrderByDescending(movement => movement.CreatedAt)
            .Take(5)
            .ToListAsync();

        var productStockMovements = await _context.ProductStockMovements
            .OrderByDescending(movement => movement.CreatedAt)
            .Take(5)
            .ToListAsync();

        var totalCalculatedMaterialCost = printJobs.Sum(printJob => printJob.CalculatedMaterialCost ?? 0);
        var totalPackagingCost = printJobs.Sum(printJob => printJob.PackagingCost ?? 0);

        decimal? EstimatedMachineCost(PrintJob printJob)
        {
            if (!printJob.PrinterId.HasValue
                || !printerCostPerHour.TryGetValue(printJob.PrinterId.Value, out var costPerHour)
                || !costPerHour.HasValue)
            {
                return null;
            }

            var machineTimeMinutes = printJob.ActualTimeMinutes ?? printJob.EstimatedTimeMinutes;
            if (!machineTimeMinutes.HasValue)
            {
                return null;
            }

            return Math.Round((machineTimeMinutes.Value / 60m) * costPerHour.Value, 2);
        }

        var totalEstimatedMachineCost = printJobs.Sum(printJob => EstimatedMachineCost(printJob) ?? 0);

        var totalEstimatedMargin = 0m;
        var productionsWithEstimatedMargin = 0;

        foreach (var printJob in printJobs)
        {
            var estimatedMachineCost = EstimatedMachineCost(printJob);
            if (!printJob.CalculatedMaterialCost.HasValue || !estimatedMachineCost.HasValue)
            {
                continue;
            }

            decimal? productSalePrice = printJob.ProductId.HasValue
                && productSalePriceById.TryGetValue(printJob.ProductId.Value, out var salePrice)
                ? salePrice
                : null;

            if (!productSalePrice.HasValue)
            {
                continue;
            }

            var estimatedTotalProductionCost = printJob.CalculatedMaterialCost.Value
                + estimatedMachineCost.Value
                + (printJob.PackagingCost ?? 0);

            totalEstimatedMargin += productSalePrice.Value - estimatedTotalProductionCost;
            productionsWithEstimatedMargin++;
        }

        var normalizedPrintJobStatuses = printJobs
            .Select(printJob => PrintJobStatus.Normalize(printJob.Status))
            .ToList();

        var viewModel = new DashboardViewModel
        {
            TotalPrintJobs = printJobs.Count,
            TotalPrintImports = printImports.Count,

            CompletedPrintJobs = normalizedPrintJobStatuses.Count(status => status == PrintJobStatus.Completed),
            FailedPrintJobs = normalizedPrintJobStatuses.Count(status => status == PrintJobStatus.Failed),
            PlannedPrintJobs = normalizedPrintJobStatuses.Count(status => status == PrintJobStatus.Planned),
            ImportedPrintJobs = normalizedPrintJobStatuses.Count(status => status == PrintJobStatus.Imported),

            ParsedPrintImports = printImports.Count(printImport =>
                PrintImportStatus.Normalize(printImport.Status) == PrintImportStatus.Parsed),
            FailedPrintImports = printImports.Count(printImport =>
                PrintImportStatus.Normalize(printImport.Status) == PrintImportStatus.Error),
            PendingProductionImports = printImports.Count(printImport =>
                PrintImportStatus.Normalize(printImport.Status) == PrintImportStatus.Parsed
                && !string.IsNullOrWhiteSpace(printImport.ParsedDataJson)
                && !linkedPrintImportIds.Contains(printImport.Id)),

            TotalFilamentUsedGrams = printJobs.Sum(printJob => printJob.FilamentUsedGrams ?? 0),
            TotalEstimatedTimeMinutes = printJobs.Sum(printJob => printJob.EstimatedTimeMinutes ?? 0),
            TotalActualTimeMinutes = printJobs.Sum(printJob => printJob.ActualTimeMinutes ?? 0),
            TotalReportedCost = printJobs.Sum(printJob => printJob.ReportedCost ?? 0),
            TotalCalculatedMaterialCost = totalCalculatedMaterialCost,
            TotalEstimatedMachineCost = totalEstimatedMachineCost,
            TotalPackagingCost = totalPackagingCost,
            TotalEstimatedProductionCost = totalCalculatedMaterialCost + totalEstimatedMachineCost + totalPackagingCost,
            TotalEstimatedMargin = totalEstimatedMargin,
            ProductionsWithEstimatedMargin = productionsWithEstimatedMargin,

            LowStockMaterialsCount = lowStockMaterials.Count,
            LowStockMaterials = lowStockMaterials
                .Select(material => new DashboardLowStockMaterialViewModel
                {
                    Id = material.Id,
                    Name = material.Name,
                    CurrentStockGrams = material.CurrentStockGrams ?? 0,
                    MinimumStockGrams = material.MinimumStockGrams ?? 0
                })
                .ToList(),

            LowStockProductsCount = lowStockProducts.Count,
            LowStockProducts = lowStockProducts
                .Select(product => new DashboardLowStockProductViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    StockQuantity = product.StockQuantity ?? 0,
                    MinimumStockQuantity = product.MinimumStockQuantity ?? 0
                })
                .ToList(),

            RecentPrintJobs = printJobs
                .Take(8)
                .Select(printJob => new DashboardRecentPrintJobViewModel
                {
                    Id = printJob.Id,

                    ProductName = printJob.ProductId.HasValue && productNames.TryGetValue(printJob.ProductId.Value, out var productName)
                        ? productName
                        : "Não vinculado",

                    MaterialName = printJob.MaterialId.HasValue && materialNames.TryGetValue(printJob.MaterialId.Value, out var materialName)
                        ? materialName
                        : "Não vinculado",

                    PrinterName = printJob.PrinterId.HasValue && printerNames.TryGetValue(printJob.PrinterId.Value, out var printerName)
                        ? printerName
                        : "Não vinculada",

                    SourceFileName = printJob.SourceFileName,
                    FilamentUsedGrams = printJob.FilamentUsedGrams,
                    EstimatedTimeMinutes = printJob.EstimatedTimeMinutes,
                    ReportedCost = printJob.ReportedCost,
                    CalculatedMaterialCost = printJob.CalculatedMaterialCost,
                    Status = printJob.Status,
                    FailureReason = printJob.FailureReason,
                    CreatedAt = printJob.CreatedAt
                })
                .ToList(),

            RecentFailedImports = printImports
                .Where(printImport => PrintImportStatus.Normalize(printImport.Status) == PrintImportStatus.Error)
                .Take(5)
                .Select(printImport => new DashboardFailedPrintImportViewModel
                {
                    Id = printImport.Id,
                    FileName = printImport.FileName,
                    Status = printImport.Status,
                    ErrorMessage = printImport.ErrorMessage,
                    CanProcessAgain = CanProcessAgain(printImport),
                    ImportedAt = printImport.ImportedAt
                })
                .ToList(),

            RecentStockMovements = stockMovements
                .Select(movement => new DashboardStockMovementViewModel
                {
                    Id = movement.Id,
                    MaterialName = materialNames.TryGetValue(movement.MaterialId, out var movementMaterialName)
                        ? movementMaterialName
                        : "Material não encontrado",
                    MovementType = movement.MovementType,
                    QuantityGrams = movement.QuantityGrams,
                    StockBeforeGrams = movement.StockBeforeGrams,
                    StockAfterGrams = movement.StockAfterGrams,
                    Notes = movement.Notes,
                    CreatedAt = movement.CreatedAt
                })
                .ToList(),

            RecentProductStockMovements = productStockMovements
                .Select(movement => new DashboardProductStockMovementViewModel
                {
                    Id = movement.Id,
                    ProductName = productNames.TryGetValue(movement.ProductId, out var movementProductName)
                        ? movementProductName
                        : "Produto não encontrado",
                    MovementType = movement.MovementType,
                    QuantityUnits = movement.QuantityUnits,
                    StockBeforeUnits = movement.StockBeforeUnits,
                    StockAfterUnits = movement.StockAfterUnits,
                    Notes = movement.Notes,
                    CreatedAt = movement.CreatedAt
                })
                .ToList()
        };

        return View(viewModel);
    }

    private bool CanProcessAgain(PrintImport printImport)
    {
        return !string.IsNullOrWhiteSpace(printImport.RawContent)
            && _parser.CanParse(printImport.FileName, printImport.RawContent);
    }
}
