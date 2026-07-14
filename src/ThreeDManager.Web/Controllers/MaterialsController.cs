using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Web.Presentation;
using ThreeDManager.Web.ViewModels;

namespace ThreeDManager.Web.Controllers;

public class MaterialsController : Controller
{
    private readonly AppDbContext _context;

    public MaterialsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? stockStatus, string? sort)
    {
        var allMaterials = await _context.Materials
            .OrderByDescending(material => material.CreatedAt)
            .ToListAsync();

        var lowStockMaterials = allMaterials
            .Where(IsLowStock)
            .ToList();

        var isLowStockFilter = string.Equals(stockStatus, "low", StringComparison.OrdinalIgnoreCase);
        var normalizedSort = NormalizeSort(sort);
        var materials = SortMaterials(isLowStockFilter ? lowStockMaterials : allMaterials, normalizedSort);

        var materialIds = allMaterials.Select(material => material.Id).ToList();
        var latestStockMovements = await _context.MaterialStockMovements
            .Where(movement => materialIds.Contains(movement.MaterialId))
            .OrderByDescending(movement => movement.CreatedAt)
            .ToListAsync();

        ViewBag.LatestStockMovements = latestStockMovements
            .GroupBy(movement => movement.MaterialId)
            .ToDictionary(group => group.Key, group => group.First());

        ViewData["StockStatusFilter"] = isLowStockFilter ? "low" : null;
        ViewData["Sort"] = normalizedSort;
        ViewData["LowStockMaterialCount"] = lowStockMaterials.Count;
        ViewData["OutOfStockMaterialCount"] = lowStockMaterials.Count(material =>
            StockStatusPresentation.IsOutOfStock(material.CurrentStockGrams, material.MinimumStockGrams));

        return View(materials);
    }

    private static bool IsLowStock(Material material)
    {
        return material.CurrentStockGrams.HasValue
            && material.MinimumStockGrams.HasValue
            && material.CurrentStockGrams.Value <= material.MinimumStockGrams.Value;
    }

    private static string? NormalizeSort(string? sort)
    {
        return sort switch
        {
            "valueDesc" => "valueDesc",
            "valueAsc" => "valueAsc",
            "costDesc" => "costDesc",
            "costAsc" => "costAsc",
            "stockDesc" => "stockDesc",
            "stockAsc" => "stockAsc",
            _ => null
        };
    }

    private static List<Material> SortMaterials(List<Material> materials, string? sort)
    {
        // Materials whose sort key cannot be computed (missing stock value or unit cost)
        // always sink to the bottom, regardless of the sort direction. The default (null)
        // sort keeps the existing newest-first ordering established by the query above.
        static decimal? Value(Material material)
            => MaterialCostPresentation.InventoryValue(material.CostPerKg, material.CurrentStockGrams);

        return sort switch
        {
            "valueDesc" => materials
                .OrderByDescending(material => Value(material).HasValue)
                .ThenByDescending(material => Value(material) ?? 0m)
                .ToList(),
            "valueAsc" => materials
                .OrderByDescending(material => Value(material).HasValue)
                .ThenBy(material => Value(material) ?? 0m)
                .ToList(),
            "costDesc" => materials
                .OrderByDescending(material => material.CostPerKg.HasValue)
                .ThenByDescending(material => material.CostPerKg ?? 0m)
                .ToList(),
            "costAsc" => materials
                .OrderByDescending(material => material.CostPerKg.HasValue)
                .ThenBy(material => material.CostPerKg ?? 0m)
                .ToList(),
            "stockDesc" => materials
                .OrderByDescending(material => material.CurrentStockGrams.HasValue)
                .ThenByDescending(material => material.CurrentStockGrams ?? 0m)
                .ToList(),
            "stockAsc" => materials
                .OrderByDescending(material => material.CurrentStockGrams.HasValue)
                .ThenBy(material => material.CurrentStockGrams ?? 0m)
                .ToList(),
            _ => materials
        };
    }

    public async Task<IActionResult> Details(Guid? id, string? returnTo)
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

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

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

    public async Task<IActionResult> Edit(Guid? id, string? returnTo)
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

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

        return View(material);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Material material, string? returnTo)
    {
        if (id != material.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
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
        existingMaterial.MinimumStockGrams = material.MinimumStockGrams;

        if (currentStockBeforeUpdate != material.CurrentStockGrams)
        {
            var stockBefore = currentStockBeforeUpdate ?? 0;
            var stockAfter = material.CurrentStockGrams ?? 0;

            _context.MaterialStockMovements.Add(new MaterialStockMovement
            {
                MaterialId = existingMaterial.Id,
                MovementType = StockMovementType.ManualAdjustment,
                QuantityGrams = stockAfter - stockBefore,
                StockBeforeGrams = currentStockBeforeUpdate,
                StockAfterGrams = material.CurrentStockGrams,
                Notes = "Ajuste manual realizado no cadastro do material.",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Material atualizado com sucesso.";

        var normalizedReturnTo = NormalizeReturnTo(returnTo);

        return normalizedReturnTo == "lowStock"
            ? RedirectToAction(nameof(Index), new { stockStatus = "low" })
            : RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> AdjustStock(Guid? id, string? returnTo)
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

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(MaterialStockAdjustmentViewModel viewModel, string? returnTo)
    {
        var material = await _context.Materials.FindAsync(viewModel.MaterialId);

        if (material is null)
        {
            return NotFound();
        }

        var normalizedReturnTo = NormalizeReturnTo(returnTo);

        if (!ModelState.IsValid)
        {
            viewModel.MaterialName = material.Name;
            viewModel.CurrentStockGrams = material.CurrentStockGrams;
            ViewData["ReturnTo"] = normalizedReturnTo;
            return View(viewModel);
        }

        var stockBefore = material.CurrentStockGrams ?? 0m;
        decimal stockAfter;
        decimal movementQuantity;

        switch (viewModel.AdjustmentType)
        {
            case StockAdjustmentType.Add:
                movementQuantity = viewModel.QuantityGrams;
                stockAfter = stockBefore + viewModel.QuantityGrams;
                break;
            case StockAdjustmentType.Remove:
                movementQuantity = -viewModel.QuantityGrams;
                stockAfter = stockBefore - viewModel.QuantityGrams;
                if (stockAfter < 0)
                {
                    ModelState.AddModelError(nameof(viewModel.QuantityGrams), "A remoção não pode deixar o estoque negativo.");
                    viewModel.MaterialName = material.Name;
                    viewModel.CurrentStockGrams = material.CurrentStockGrams;
                    ViewData["ReturnTo"] = normalizedReturnTo;
                    return View(viewModel);
                }
                break;
            case StockAdjustmentType.Set:
                stockAfter = viewModel.QuantityGrams;
                movementQuantity = stockAfter - stockBefore;
                break;
            default:
                ModelState.AddModelError(nameof(viewModel.AdjustmentType), "Tipo de ajuste inválido.");
                viewModel.MaterialName = material.Name;
                viewModel.CurrentStockGrams = material.CurrentStockGrams;
                ViewData["ReturnTo"] = normalizedReturnTo;
                return View(viewModel);
        }

        if (movementQuantity == 0m)
        {
            // A "definir" (Set) adjustment to the current value changes nothing; mirror the
            // Edit flow's guard and skip persisting a misleading zero-quantity ledger entry.
            TempData["SuccessMessage"] = "O estoque informado já é o atual; nenhum ajuste foi registrado.";
            return RedirectToAction(nameof(Details), new { id = material.Id, returnTo = normalizedReturnTo });
        }

        material.CurrentStockGrams = stockAfter;

        _context.MaterialStockMovements.Add(new MaterialStockMovement
        {
            MaterialId = material.Id,
            MovementType = StockMovementType.ManualAdjustment,
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
        return RedirectToAction(nameof(Details), new { id = material.Id, returnTo = normalizedReturnTo });
    }

    public async Task<IActionResult> Delete(Guid? id, string? returnTo)
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

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

        return View(material);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? returnTo)
    {
        var material = await _context.Materials.FindAsync(id);

        if (material is null)
        {
            return NotFound();
        }

        var normalizedReturnTo = NormalizeReturnTo(returnTo);

        var hasPrintJobs = await _context.PrintJobs
            .AnyAsync(printJob => printJob.MaterialId == id);

        if (hasPrintJobs)
        {
            TempData["ErrorMessage"] = "Este material não pode ser removido porque possui produções vinculadas.";
            return normalizedReturnTo == "lowStock"
                ? RedirectToAction(nameof(Index), new { stockStatus = "low" })
                : RedirectToAction(nameof(Index));
        }

        _context.Materials.Remove(material);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Material removido com sucesso.";

        return normalizedReturnTo == "lowStock"
            ? RedirectToAction(nameof(Index), new { stockStatus = "low" })
            : RedirectToAction(nameof(Index));
    }

    private static string? NormalizeReturnTo(string? returnTo)
    {
        if (string.Equals(returnTo, "lowStock", StringComparison.OrdinalIgnoreCase))
        {
            return "lowStock";
        }

        return null;
    }
}
