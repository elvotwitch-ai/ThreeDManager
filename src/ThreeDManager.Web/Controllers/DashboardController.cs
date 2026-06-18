using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        var productNames = await _context.Products
            .ToDictionaryAsync(product => product.Id, product => product.Name);

        var materialNames = await _context.Materials
            .ToDictionaryAsync(material => material.Id, material => material.Name);

        var printerNames = await _context.Printers
            .ToDictionaryAsync(printer => printer.Id, printer => printer.Name);

        var viewModel = new DashboardViewModel
        {
            TotalPrintJobs = printJobs.Count,
            TotalPrintImports = printImports.Count,

            CompletedPrintJobs = printJobs.Count(printJob => printJob.Status == "Completed"),
            FailedPrintJobs = printJobs.Count(printJob => printJob.Status == "Failed"),
            PlannedPrintJobs = printJobs.Count(printJob => printJob.Status == "Planned"),
            ImportedPrintJobs = printJobs.Count(printJob => printJob.Status == "Imported"),

            ParsedPrintImports = printImports.Count(printImport => printImport.Status == "Parsed"),
            FailedPrintImports = printImports.Count(printImport => printImport.Status == "Error"),

            TotalFilamentUsedGrams = printJobs.Sum(printJob => printJob.FilamentUsedGrams ?? 0),
            TotalEstimatedTimeMinutes = printJobs.Sum(printJob => printJob.EstimatedTimeMinutes ?? 0),
            TotalActualTimeMinutes = printJobs.Sum(printJob => printJob.ActualTimeMinutes ?? 0),
            TotalReportedCost = printJobs.Sum(printJob => printJob.ReportedCost ?? 0),

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
                    Status = printJob.Status,
                    CreatedAt = printJob.CreatedAt
                })
                .ToList(),

            RecentFailedImports = printImports
                .Where(printImport => printImport.Status == "Error")
                .Take(5)
                .Select(printImport => new DashboardFailedPrintImportViewModel
                {
                    Id = printImport.Id,
                    FileName = printImport.FileName,
                    Status = printImport.Status,
                    ErrorMessage = printImport.ErrorMessage,
                    ImportedAt = printImport.ImportedAt
                })
                .ToList()
        };

        return View(viewModel);
    }
}
