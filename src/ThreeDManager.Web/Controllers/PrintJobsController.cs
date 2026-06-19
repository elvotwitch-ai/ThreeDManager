using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using ThreeDManager.Domain.Entities;

namespace ThreeDManager.Web.Controllers;

public class PrintJobsController : Controller
{
    private readonly AppDbContext _context;

    public PrintJobsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var printJobs = await _context.PrintJobs
            .OrderByDescending(printJob => printJob.CreatedAt)
            .ToListAsync();

        ViewBag.Products = await _context.Products
            .ToDictionaryAsync(product => product.Id, product => product.Name);

        ViewBag.Materials = await _context.Materials
            .ToDictionaryAsync(material => material.Id, material => material.Name);

        ViewBag.Printers = await _context.Printers
            .ToDictionaryAsync(printer => printer.Id, printer => printer.Name);

        return View(printJobs);
    }

    public async Task<IActionResult> Details(Guid? id)
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

        ViewBag.MaterialName = printJob.MaterialId.HasValue
            ? await _context.Materials
                .Where(material => material.Id == printJob.MaterialId.Value)
                .Select(material => material.Name)
                .FirstOrDefaultAsync()
            : null;

        ViewBag.PrinterName = printJob.PrinterId.HasValue
            ? await _context.Printers
                .Where(printer => printer.Id == printJob.PrinterId.Value)
                .Select(printer => printer.Name)
                .FirstOrDefaultAsync()
            : null;

        return View(printJob);
    }
    public async Task<IActionResult> Edit(Guid? id)
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

        return View(printJob);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, PrintJob printJob)
    {
        if (id != printJob.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(printJob);
            return View(printJob);
        }

        var existingPrintJob = await _context.PrintJobs.FindAsync(id);

        if (existingPrintJob is null)
        {
            return NotFound();
        }

        await RestoreStockDeductionAsync(existingPrintJob);

        existingPrintJob.ProductId = printJob.ProductId;
        existingPrintJob.PrinterId = printJob.PrinterId;
        existingPrintJob.MaterialId = printJob.MaterialId;
        existingPrintJob.FilamentUsedGrams = printJob.FilamentUsedGrams;
        existingPrintJob.FilamentUsedMeters = printJob.FilamentUsedMeters;
        existingPrintJob.EstimatedTimeMinutes = printJob.EstimatedTimeMinutes;
        existingPrintJob.ActualTimeMinutes = printJob.ActualTimeMinutes;
        existingPrintJob.ReportedCost = printJob.ReportedCost;
        existingPrintJob.CalculatedMaterialCost = await CalculateMaterialCostAsync(
            printJob.MaterialId,
            printJob.FilamentUsedGrams);
        existingPrintJob.Status = printJob.Status;

        if (!await TryApplyStockDeductionAsync(existingPrintJob))
        {
            await PopulateOptionsAsync(printJob);
            return View(printJob);
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Produção atualizada com sucesso.";

        return RedirectToAction(nameof(Details), new { id = existingPrintJob.Id });
    }

    public async Task<IActionResult> Delete(Guid? id)
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

        return View(printJob);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var printJob = await _context.PrintJobs.FindAsync(id);

        if (printJob is null)
        {
            return NotFound();
        }

        await RestoreStockDeductionAsync(printJob);

        _context.PrintJobs.Remove(printJob);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Produção removida com sucesso.";

        return RedirectToAction(nameof(Index));
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

    private async Task<bool> TryApplyStockDeductionAsync(PrintJob printJob)
    {
        if (printJob.Status != "Completed")
        {
            return true;
        }

        if (printJob.MaterialId is null)
        {
            ModelState.AddModelError(nameof(PrintJob.MaterialId), "Selecione um material para concluir e baixar estoque.");
            return false;
        }

        if (printJob.FilamentUsedGrams is null or <= 0)
        {
            ModelState.AddModelError(nameof(PrintJob.FilamentUsedGrams), "Informe o filamento usado para concluir e baixar estoque.");
            return false;
        }

        var material = await _context.Materials.FindAsync(printJob.MaterialId.Value);

        if (material is null)
        {
            ModelState.AddModelError(nameof(PrintJob.MaterialId), "Material não encontrado para baixar estoque.");
            return false;
        }

        if (material.CurrentStockGrams is null)
        {
            ModelState.AddModelError(nameof(PrintJob.MaterialId), "O material selecionado não possui estoque informado.");
            return false;
        }

        if (material.CurrentStockGrams.Value < printJob.FilamentUsedGrams.Value)
        {
            ModelState.AddModelError(nameof(PrintJob.FilamentUsedGrams), "Estoque insuficiente para concluir esta produção.");
            return false;
        }

        material.CurrentStockGrams -= printJob.FilamentUsedGrams.Value;
        printJob.StockDeductedAt = DateTime.UtcNow;
        printJob.StockDeductedMaterialId = material.Id;
        printJob.StockDeductedGrams = printJob.FilamentUsedGrams;

        return true;
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
}
