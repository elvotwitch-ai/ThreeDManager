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

        existingPrintJob.ProductId = printJob.ProductId;
        existingPrintJob.PrinterId = printJob.PrinterId;
        existingPrintJob.MaterialId = printJob.MaterialId;
        existingPrintJob.FilamentUsedGrams = printJob.FilamentUsedGrams;
        existingPrintJob.FilamentUsedMeters = printJob.FilamentUsedMeters;
        existingPrintJob.EstimatedTimeMinutes = printJob.EstimatedTimeMinutes;
        existingPrintJob.ActualTimeMinutes = printJob.ActualTimeMinutes;
        existingPrintJob.ReportedCost = printJob.ReportedCost;
        existingPrintJob.Status = printJob.Status;

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

        _context.PrintJobs.Remove(printJob);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Produção removida com sucesso.";

        return RedirectToAction(nameof(Index));
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
}