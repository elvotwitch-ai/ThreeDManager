using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;

namespace ThreeDManager.Web.Controllers;

public class ProductsController : Controller
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var products = await _context.Products
            .OrderByDescending(product => product.CreatedAt)
            .ToListAsync();

        return View(products);
    }

    public async Task<IActionResult> Details(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(product => product.Id == id);

        if (product is null)
        {
            return NotFound();
        }

        return View(product);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product)
    {
        if (!ModelState.IsValid)
        {
            return View(product);
        }

        product.Id = Guid.NewGuid();
        product.CreatedAt = DateTime.UtcNow;
        product.IsActive = true;

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var product = await _context.Products.FindAsync(id);

        if (product is null)
        {
            return NotFound();
        }

        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Product product)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(product);
        }

        var existingProduct = await _context.Products.FindAsync(id);

        if (existingProduct is null)
        {
            return NotFound();
        }

        existingProduct.Name = product.Name;
        existingProduct.Sku = product.Sku;
        existingProduct.Category = product.Category;
        existingProduct.Description = product.Description;
        existingProduct.SalePrice = product.SalePrice;
        existingProduct.StockQuantity = product.StockQuantity;
        existingProduct.MinimumStockQuantity = product.MinimumStockQuantity;
        existingProduct.IsActive = product.IsActive;

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var product = await _context.Products.FindAsync(id);

        if (product is null)
        {
            return NotFound();
        }

        product.IsActive = false;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id)
    {
        var product = await _context.Products.FindAsync(id);

        if (product is null)
        {
            return NotFound();
        }

        product.IsActive = true;
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var product = await _context.Products
            .FirstOrDefaultAsync(product => product.Id == id);

        if (product is null)
        {
            return NotFound();
        }

        return View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var product = await _context.Products.FindAsync(id);

        if (product is null)
        {
            return NotFound();
        }

        var hasPrintJobs = await _context.PrintJobs
            .AnyAsync(printJob => printJob.ProductId == id);

        if (hasPrintJobs)
        {
            TempData["ErrorMessage"] = "Este produto não pode ser removido porque possui produções vinculadas. Desative o produto em vez de removê-lo.";
            return RedirectToAction(nameof(Index));
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Produto removido com sucesso.";
        return RedirectToAction(nameof(Index));
    }
}
