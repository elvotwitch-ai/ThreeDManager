using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Web.ViewModels;

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

        var materialIds = materials.Select(material => material.Id).ToList();
        var latestStockMovements = await _context.MaterialStockMovements
            .Where(movement => materialIds.Contains(movement.MaterialId))
            .OrderByDescending(movement => movement.CreatedAt)
            .ToListAsync();

        ViewBag.LatestStockMovements = latestStockMovements
            .GroupBy(movement => movement.MaterialId)
            .ToDictionary(group => group.Key, group => group.First());

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

            _context.MaterialStockMovements.Add(new MaterialStockMovement
            {
                MaterialId = existingMaterial.Id,
                MovementType = "ManualAdjustment",
                QuantityGrams = stockAfter - stockBefore,
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

    public async Task<IActionResult> AdjustStock(Guid? id)
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

        var viewModel = new MaterialStockAdjustmentViewModel
        {
            MaterialId = material.Id,
            MaterialName = material.Name,
            CurrentStockGrams = material.CurrentStockGrams
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(MaterialStockAdjustmentViewModel viewModel)
    {
        var material = await _context.Materials.FindAsync(viewModel.MaterialId);

        if (material is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            viewModel.MaterialName = material.Name;
            viewModel.CurrentStockGrams = material.CurrentStockGrams;
            return View(viewModel);
        }

        var stockBefore = material.CurrentStockGrams ?? 0m;
        decimal stockAfter;
        decimal movementQuantity;

        switch (viewModel.AdjustmentType)
        {
            case "Add":
                movementQuantity = viewModel.QuantityGrams;
                stockAfter = stockBefore + viewModel.QuantityGrams;
                break;
            case "Remove":
                movementQuantity = -viewModel.QuantityGrams;
                stockAfter = stockBefore - viewModel.QuantityGrams;
                if (stockAfter < 0)
                {
                    ModelState.AddModelError(nameof(viewModel.QuantityGrams), "A remoção não pode deixar o estoque negativo.");
                    viewModel.MaterialName = material.Name;
                    viewModel.CurrentStockGrams = material.CurrentStockGrams;
                    return View(viewModel);
                }
                break;
            case "Set":
                stockAfter = viewModel.QuantityGrams;
                movementQuantity = stockAfter - stockBefore;
                break;
            default:
                ModelState.AddModelError(nameof(viewModel.AdjustmentType), "Tipo de ajuste inválido.");
                viewModel.MaterialName = material.Name;
                viewModel.CurrentStockGrams = material.CurrentStockGrams;
                return View(viewModel);
        }

        material.CurrentStockGrams = stockAfter;

        _context.MaterialStockMovements.Add(new MaterialStockMovement
        {
            MaterialId = material.Id,
            MovementType = "ManualAdjustment",
            QuantityGrams = movementQuantity,
            StockBeforeGrams = stockBefore,
            StockAfterGrams = stockAfter,
            Notes = string.IsNullOrWhiteSpace(viewModel.Notes)
                ? $"Ajuste manual de estoque ({movementQuantity.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} g)."
                : viewModel.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Estoque ajustado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = material.Id });
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
