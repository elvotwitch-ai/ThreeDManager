using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Application.Interfaces;
using ThreeDManager.Web.Presentation;

namespace ThreeDManager.Web.Controllers;

public class PrintJobsController : Controller
{
    private readonly AppDbContext _context;
    private readonly IPrintJobStockService _stockService;

    public PrintJobsController(
        AppDbContext context,
        IPrintJobStockService stockService)
    {
        _context = context;
        _stockService = stockService;
    }

    public async Task<IActionResult> Index(Guid? printerId, string? status, string? sort)
    {
        var printJobsQuery = _context.PrintJobs.AsQueryable();

        if (printerId.HasValue)
        {
            printJobsQuery = printJobsQuery.Where(printJob => printJob.PrinterId == printerId.Value);
        }

        var allPrintJobs = await printJobsQuery
            .OrderByDescending(printJob => printJob.CreatedAt)
            .ToListAsync();

        var failedPrintJobs = allPrintJobs
            .Where(printJob => PrintJobStatus.Normalize(printJob.Status) == PrintJobStatus.Failed)
            .ToList();

        var isFailedStatusFilter = string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);

        var selectedPrintJobs = isFailedStatusFilter ? failedPrintJobs : allPrintJobs;

        var normalizedSort = NormalizeSort(sort);
        var printJobs = SortPrintJobs(selectedPrintJobs, normalizedSort);
        ViewData["Sort"] = normalizedSort;

        ViewData["StatusFilter"] = isFailedStatusFilter ? "failed" : null;
        ViewData["FailedPrintJobCount"] = failedPrintJobs.Count;

        ViewBag.Products = await _context.Products
            .ToDictionaryAsync(product => product.Id, product => product.Name);

        ViewBag.ProductSalePriceById = await _context.Products
            .ToDictionaryAsync(product => product.Id, product => product.SalePrice);

        ViewBag.ProductTargetMarginPercentageById = await _context.Products
            .ToDictionaryAsync(product => product.Id, product => product.TargetMarginPercentage);

        ViewBag.Materials = await _context.Materials
            .ToDictionaryAsync(material => material.Id, material => material.Name);

        var printers = await _context.Printers.ToListAsync();
        ViewBag.Printers = printers.ToDictionary(printer => printer.Id, printer => printer.Name);
        ViewBag.PrinterCostPerHour = printers.ToDictionary(printer => printer.Id, printer => printer.CostPerHour);

        if (printerId.HasValue)
        {
            ViewData["PrinterFilterId"] = printerId.Value;
            ViewData["PrinterFilterName"] = printers
                .FirstOrDefault(printer => printer.Id == printerId.Value)?.Name;
        }

        return View(printJobs);
    }

    private static string? NormalizeSort(string? sort)
    {
        return sort switch
        {
            "filamentDesc" => "filamentDesc",
            "filamentAsc" => "filamentAsc",
            "materialCostDesc" => "materialCostDesc",
            "materialCostAsc" => "materialCostAsc",
            _ => null
        };
    }

    private static List<PrintJob> SortPrintJobs(List<PrintJob> printJobs, string? sort)
    {
        // Print jobs with no recorded value for the sorted column always sink to the bottom,
        // regardless of the sort direction. The default (null) sort keeps the existing
        // newest-first ordering established by the query above.
        return sort switch
        {
            "filamentDesc" => printJobs
                .OrderByDescending(printJob => printJob.FilamentUsedGrams.HasValue)
                .ThenByDescending(printJob => printJob.FilamentUsedGrams ?? 0m)
                .ToList(),
            "filamentAsc" => printJobs
                .OrderByDescending(printJob => printJob.FilamentUsedGrams.HasValue)
                .ThenBy(printJob => printJob.FilamentUsedGrams ?? 0m)
                .ToList(),
            "materialCostDesc" => printJobs
                .OrderByDescending(printJob => printJob.CalculatedMaterialCost.HasValue)
                .ThenByDescending(printJob => printJob.CalculatedMaterialCost ?? 0m)
                .ToList(),
            "materialCostAsc" => printJobs
                .OrderByDescending(printJob => printJob.CalculatedMaterialCost.HasValue)
                .ThenBy(printJob => printJob.CalculatedMaterialCost ?? 0m)
                .ToList(),
            _ => printJobs
        };
    }

    public async Task<IActionResult> Details(Guid? id, string? returnTo, Guid? filterPrinterId)
    {
        if (id is null)
        {
            return NotFound();
        }

        var printJob = await _context.PrintJobs
            .FirstOrDefaultAsync(printJob => printJob.Id == id);

        if (printJob is null)
        {
            return NotFound();
        }

        ViewBag.ProductName = printJob.ProductId.HasValue
            ? await _context.Products
                .Where(product => product.Id == printJob.ProductId.Value)
                .Select(product => product.Name)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.ProductSalePrice = printJob.ProductId.HasValue
            ? await _context.Products
                .Where(product => product.Id == printJob.ProductId.Value)
                .Select(product => product.SalePrice)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.ProductTargetMarginPercentage = printJob.ProductId.HasValue
            ? await _context.Products
                .Where(product => product.Id == printJob.ProductId.Value)
                .Select(product => product.TargetMarginPercentage)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.MaterialName = printJob.MaterialId.HasValue
            ? await _context.Materials
                .Where(material => material.Id == printJob.MaterialId.Value)
                .Select(material => material.Name)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.MaterialCostPerKg = printJob.MaterialId.HasValue
            ? await _context.Materials
                .Where(material => material.Id == printJob.MaterialId.Value)
                .Select(material => material.CostPerKg)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.PrinterName = printJob.PrinterId.HasValue
            ? await _context.Printers
                .Where(printer => printer.Id == printJob.PrinterId.Value)
                .Select(printer => printer.Name)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.PrinterCostPerHour = printJob.PrinterId.HasValue
            ? await _context.Printers
                .Where(printer => printer.Id == printJob.PrinterId.Value)
                .Select(printer => printer.CostPerHour)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.StockMovement = await _context.MaterialStockMovements
            .Where(movement => movement.PrintJobId == printJob.Id)
            .OrderByDescending(movement => movement.CreatedAt)
            .FirstOrDefaultAsync();

        ViewBag.ProductStockMovement = await _context.ProductStockMovements
            .Where(movement => movement.PrintJobId == printJob.Id)
            .OrderByDescending(movement => movement.CreatedAt)
            .FirstOrDefaultAsync();
        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
        ViewData["ReturnPrinterId"] = filterPrinterId;

        return View(printJob);
    }
    public async Task<IActionResult> Edit(Guid? id, string? returnTo, Guid? filterPrinterId)
    {
        if (id is null)
        {
            return NotFound();
        }

        var printJob = await _context.PrintJobs.FindAsync(id);

        if (printJob is null)
        {
            return NotFound();
        }

        await PopulateOptionsAsync(printJob);
        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
        ViewData["ReturnPrinterId"] = filterPrinterId;

        return View(printJob);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, PrintJob printJob, string? returnTo, Guid? filterPrinterId)
    {
        if (id != printJob.Id)
        {
            return BadRequest();
        }

        var normalizedStatus = PrintJobStatus.Normalize(printJob.Status);

        if (normalizedStatus is null)
        {
            ModelState.AddModelError(nameof(PrintJob.Status), "Selecione um status de produção válido.");
        }
        else
        {
            printJob.Status = normalizedStatus;

            if (normalizedStatus == PrintJobStatus.Failed && string.IsNullOrWhiteSpace(printJob.FailureReason))
            {
                ModelState.AddModelError(nameof(PrintJob.FailureReason), "Informe o motivo da falha para marcar a produção como falha.");
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(printJob);
            ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
            ViewData["ReturnPrinterId"] = filterPrinterId;
            return View(printJob);
        }

        var existingPrintJob = await _context.PrintJobs.FindAsync(id);

        if (existingPrintJob is null)
        {
            return NotFound();
        }

        var stockResult = await _stockService.SyncForEditedPrintJobAsync(
            existingPrintJob,
            printJob.MaterialId,
            printJob.FilamentUsedGrams,
            printJob.Status,
            printJob.ProductId,
            printJob.UnitsProduced);

        existingPrintJob.ProductId = printJob.ProductId;
        existingPrintJob.PrinterId = printJob.PrinterId;
        existingPrintJob.MaterialId = printJob.MaterialId;
        existingPrintJob.FilamentUsedGrams = printJob.FilamentUsedGrams;
        existingPrintJob.FilamentUsedMeters = printJob.FilamentUsedMeters;
        existingPrintJob.EstimatedTimeMinutes = printJob.EstimatedTimeMinutes;
        existingPrintJob.ActualTimeMinutes = printJob.ActualTimeMinutes;
        existingPrintJob.ReportedCost = printJob.ReportedCost;
        existingPrintJob.PackagingCost = printJob.PackagingCost;
        existingPrintJob.UnitsProduced = printJob.UnitsProduced;
        existingPrintJob.CalculatedMaterialCost = await CalculateMaterialCostAsync(
            printJob.MaterialId,
            printJob.FilamentUsedGrams);
        existingPrintJob.Status = printJob.Status;
        existingPrintJob.FailureReason = printJob.FailureReason;

        if (!stockResult.Succeeded)
        {
            AddStockModelError(stockResult);
            await PopulateOptionsAsync(printJob);
            ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
            ViewData["ReturnPrinterId"] = filterPrinterId;
            return View(printJob);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Produção atualizada com sucesso.";

        return RedirectToAction(
            nameof(Details),
            new
            {
                id = existingPrintJob.Id,
                returnTo = NormalizeReturnTo(returnTo),
                filterPrinterId
            });
    }

    public async Task<IActionResult> Delete(Guid? id, string? returnTo, Guid? filterPrinterId)
    {
        if (id is null)
        {
            return NotFound();
        }

        var printJob = await _context.PrintJobs
            .FirstOrDefaultAsync(printJob => printJob.Id == id);

        if (printJob is null)
        {
            return NotFound();
        }

        ViewBag.ProductName = printJob.ProductId.HasValue
            ? await _context.Products
                .Where(product => product.Id == printJob.ProductId.Value)
                .Select(product => product.Name)
                .FirstOrDefaultAsync()
            : null;
        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
        ViewData["ReturnPrinterId"] = filterPrinterId;

        return View(printJob);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? returnTo, Guid? filterPrinterId)
    {
        var printJob = await _context.PrintJobs.FindAsync(id);

        if (printJob is null)
        {
            return NotFound();
        }

        await _stockService.RestoreForDeletedPrintJobAsync(printJob);

        _context.PrintJobs.Remove(printJob);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Produção removida com sucesso.";

        var normalizedReturnTo = NormalizeReturnTo(returnTo);

        if (normalizedReturnTo == "failedQueue")
        {
            return RedirectToAction(nameof(Index), new { status = "failed", printerId = filterPrinterId });
        }

        if (printJob.PrintImportId.HasValue && normalizedReturnTo is not null)
        {
            return RedirectToAction(
                "Details",
                "PrintImports",
                new
                {
                    id = printJob.PrintImportId.Value,
                    returnTo = normalizedReturnTo
                });
        }

        return RedirectToAction(nameof(Index), new { printerId = filterPrinterId });
    }

    private async Task PopulateOptionsAsync(PrintJob printJob)
    {
        var products = await _context.Products
            .Where(product => product.IsActive)
            .OrderBy(product => product.Name)
            .ToListAsync();

        var printers = await _context.Printers
            .OrderBy(printer => printer.Name)
            .ToListAsync();

        var materials = await _context.Materials
            .OrderBy(material => material.Name)
            .ToListAsync();

        ViewBag.ProductOptions = new SelectList(products, "Id", "Name", printJob.ProductId);
        ViewBag.PrinterOptions = new SelectList(printers, "Id", "Name", printJob.PrinterId);
        ViewBag.MaterialOptions = new SelectList(materials, "Id", "Name", printJob.MaterialId);
        ViewBag.ProductTargetMarginPercentageById = products.ToDictionary(
            product => product.Id.ToString(),
            product => product.TargetMarginPercentage);
        ViewBag.MaterialCostPerGramById = materials.ToDictionary(
            material => material.Id.ToString(),
            material => material.CostPerKg.HasValue ? material.CostPerKg.Value / 1000m : (decimal?)null);
        ViewBag.PrinterCostPerHourById = printers.ToDictionary(
            printer => printer.Id.ToString(),
            printer => printer.CostPerHour);
        ViewBag.StatusOptions = new SelectList(
            PrintJobStatus.All.Select(status => new
            {
                Value = status,
                Text = PrintJobStatusPresentation.GetDisplayLabel(status)
            }),
            "Value",
            "Text",
            printJob.Status);
    }

    private async Task<decimal?> CalculateMaterialCostAsync(Guid? materialId, decimal? filamentUsedGrams)
    {
        if (materialId is null || filamentUsedGrams is null)
        {
            return null;
        }

        var costPerKg = await _context.Materials
            .Where(material => material.Id == materialId.Value)
            .Select(material => material.CostPerKg)
            .FirstOrDefaultAsync();

        if (costPerKg is null)
        {
            return null;
        }

        return Math.Round((filamentUsedGrams.Value / 1000m) * costPerKg.Value, 2);
    }

    private void AddStockModelError(PrintJobStockResult stockResult)
    {
        ModelState.AddModelError(
            stockResult.FieldName ?? string.Empty,
            stockResult.ErrorMessage ?? "Não foi possível atualizar o estoque.");
    }

    private static string? NormalizeReturnTo(string? returnTo)
    {
        if (string.Equals(returnTo, "errorQueue", StringComparison.OrdinalIgnoreCase))
        {
            return "errorQueue";
        }

        if (string.Equals(returnTo, "pendingQueue", StringComparison.OrdinalIgnoreCase))
        {
            return "pendingQueue";
        }

        if (string.Equals(returnTo, "failedQueue", StringComparison.OrdinalIgnoreCase))
        {
            return "failedQueue";
        }

        return null;
    }
}
