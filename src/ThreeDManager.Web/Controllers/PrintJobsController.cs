using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Infrastructure.Data;

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
}