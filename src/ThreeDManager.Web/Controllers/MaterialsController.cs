using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;

namespace ThreeDManager.Web.Controllers;

public class MaterialsController : Controller
{
    private readonly AppDbContext _context;

    public MaterialsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var materials = await _context.Materials
            .OrderByDescending(material => material.CreatedAt)
            .ToListAsync();

        return View(materials);
    }

    public async Task<IActionResult> Details(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var material = await _context.Materials
            .FirstOrDefaultAsync(material => material.Id == id);

        if (material is null)
        {
            return NotFound();
        }

        ViewBag.StockMovements = await _context.MaterialStockMovements
            .Where(movement => movement.MaterialId == material.Id)
            .OrderByDescending(movement => movement.CreatedAt)
            .Take(20)
            .ToListAsync();

        return View(material);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Material material)
    {
        if (!ModelState.IsValid)
        {
            return View(material);
        }

        material.Id = Guid.NewGuid();
        material.CreatedAt = DateTime.UtcNow;

        _context.Materials.Add(material);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Material cadastrado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var material = await _context.Materials.FindAsync(id);

        if (material is null)
        {
            return NotFound();
        }

        return View(material);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Material material)
    {
        if (id != material.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(material);
        }

        var existingMaterial = await _context.Materials.FindAsync(id);

        if (existingMaterial is null)
        {
            return NotFound();
        }

        var currentStockBeforeUpdate = existingMaterial.CurrentStockGrams;

        existingMaterial.Name = material.Name;
        existingMaterial.Type = material.Type;
        existingMaterial.Brand = material.Brand;
        existingMaterial.Color = material.Color;
        existingMaterial.CostPerKg = material.CostPerKg;
        existingMaterial.CurrentStockGrams = material.CurrentStockGrams;

        if (currentStockBeforeUpdate != material.CurrentStockGrams)
        {
            var stockBefore = currentStockBeforeUpdate ?? 0;
            var stockAfter = material.CurrentStockGrams ?? 0;
            var quantity = stockAfter - stockBefore;

            _context.MaterialStockMovements.Add(new MaterialStockMovement
            {
                MaterialId = existingMaterial.Id,
                MovementType = "ManualAdjustment",
                QuantityGrams = quantity,
                StockBeforeGrams = currentStockBeforeUpdate,
                StockAfterGrams = material.CurrentStockGrams,
                Notes = "Ajuste manual realizado no cadastro do material.",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Material atualizado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(Guid? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var material = await _context.Materials
            .FirstOrDefaultAsync(material => material.Id == id);

        if (material is null)
        {
            return NotFound();
        }

        return View(material);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var material = await _context.Materials.FindAsync(id);

        if (material is null)
        {
            return NotFound();
        }

        var hasPrintJobs = await _context.PrintJobs
            .AnyAsync(printJob => printJob.MaterialId == id);

        if (hasPrintJobs)
        {
            TempData["ErrorMessage"] = "Este material não pode ser removido porque possui produções vinculadas.";
            return RedirectToAction(nameof(Index));
        }

        _context.Materials.Remove(material);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Material removido com sucesso.";
        return RedirectToAction(nameof(Index));
    }
}
