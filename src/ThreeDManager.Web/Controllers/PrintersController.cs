using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Web.ViewModels;

namespace ThreeDManager.Web.Controllers;

public class PrintersController : Controller
{
    private readonly AppDbContext _context;

    public PrintersController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var printers = await _context.Printers
            .OrderByDescending(printer => printer.CreatedAt)
            .ToListAsync();

        var queuedPrintJobs = await _context.PrintJobs
            .Where(printJob => printJob.PrinterId.HasValue)
            .Select(printJob => new { printJob.PrinterId, printJob.Status, printJob.EstimatedTimeMinutes })
            .ToListAsync();

        ViewBag.PrinterQueue = queuedPrintJobs
            .Where(printJob =>
            {
                var normalizedStatus = PrintJobStatus.Normalize(printJob.Status);
                return normalizedStatus == PrintJobStatus.Imported || normalizedStatus == PrintJobStatus.Planned;
            })
            .GroupBy(printJob => printJob.PrinterId!.Value)
            .ToDictionary(
                group => group.Key,
                group => new PrinterQueueSummaryViewModel
                {
                    QueuedJobsCount = group.Count(),
                    QueuedEstimatedTimeMinutes = group.Sum(printJob => printJob.EstimatedTimeMinutes ?? 0)
                });

        return View(printers);
    }

    public async Task<IActionResult> Details(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var printer = await _context.Printers
            .FirstOrDefaultAsync(printer => printer.Id == id);

        if (printer is null)
        {
            return NotFound();
        }

        return View(printer);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Printer printer)
    {
        if (!ModelState.IsValid)
        {
            return View(printer);
        }

        printer.Id = Guid.NewGuid();
        printer.CreatedAt = DateTime.UtcNow;

        _context.Printers.Add(printer);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Impressora cadastrada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var printer = await _context.Printers.FindAsync(id);

        if (printer is null)
        {
            return NotFound();
        }

        return View(printer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Printer printer)
    {
        if (id != printer.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(printer);
        }

        var existingPrinter = await _context.Printers.FindAsync(id);

        if (existingPrinter is null)
        {
            return NotFound();
        }

        existingPrinter.Name = printer.Name;
        existingPrinter.Model = printer.Model;
        existingPrinter.Brand = printer.Brand;
        existingPrinter.CostPerHour = printer.CostPerHour;
        existingPrinter.Notes = printer.Notes;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Impressora atualizada com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var printer = await _context.Printers
            .FirstOrDefaultAsync(printer => printer.Id == id);

        if (printer is null)
        {
            return NotFound();
        }

        return View(printer);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var printer = await _context.Printers.FindAsync(id);

        if (printer is null)
        {
            return NotFound();
        }

        var hasPrintJobs = await _context.PrintJobs
            .AnyAsync(printJob => printJob.PrinterId == id);

        if (hasPrintJobs)
        {
            TempData["ErrorMessage"] = "Esta impressora não pode ser removida porque possui produções vinculadas.";
            return RedirectToAction(nameof(Index));
        }

        _context.Printers.Remove(printer);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Impressora removida com sucesso.";
        return RedirectToAction(nameof(Index));
    }
}