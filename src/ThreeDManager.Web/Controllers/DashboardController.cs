using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Web.ViewModels;

namespace ThreeDManager.Web.Controllers;

public class DashboardController : Controller
{
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
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

        var lowStockMaterials = await _context.Materials
            .Where(material =>
                material.CurrentStockGrams.HasValue
                && material.MinimumStockGrams.HasValue
                && material.CurrentStockGrams.Value <= material.MinimumStockGrams.Value)
            .OrderBy(material => material.CurrentStockGrams)
            .ToListAsync();

        var stockMovements = await _context.MaterialStockMovements
            .OrderByDescending(movement => movement.CreatedAt)
            .Take(5)
            .ToListAsync();

        var viewModel = new DashboardViewModel
        {
            TotalPrintJobs = printJobs.Count,
            TotalPrintImports = printImports.Count,

            CompletedPrintJobs = printJobs.Count(printJob => printJob.Status == PrintJobStatus.Completed),
            FailedPrintJobs = printJobs.Count(printJob => printJob.Status == PrintJobStatus.Failed),
            PlannedPrintJobs = printJobs.Count(printJob => printJob.Status == PrintJobStatus.Planned),
            ImportedPrintJobs = printJobs.Count(printJob => printJob.Status == PrintJobStatus.Imported),

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
            TotalCalculatedMaterialCost = printJobs.Sum(printJob => printJob.CalculatedMaterialCost ?? 0),

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
                .ToList()
        };

        return View(viewModel);
    }
}
