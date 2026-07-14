using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Web.Presentation;
using ThreeDManager.Web.ViewModels;

namespace ThreeDManager.Web.Controllers;

public class ProductsController : Controller
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? stockStatus, string? sort)
    {
        var allProducts = await _context.Products
            .OrderByDescending(product => product.CreatedAt)
            .ToListAsync();

        var lowStockProducts = allProducts
            .Where(IsLowStock)
            .ToList();

        var isLowStockFilter = string.Equals(stockStatus, "low", StringComparison.OrdinalIgnoreCase);
        var normalizedSort = NormalizeSort(sort);
        var products = SortProducts(isLowStockFilter ? lowStockProducts : allProducts, normalizedSort);

        var productIds = allProducts.Select(product => product.Id).ToList();
        var latestStockMovements = await _context.ProductStockMovements
            .Where(movement => productIds.Contains(movement.ProductId))
            .OrderByDescending(movement => movement.CreatedAt)
            .ToListAsync();

        ViewBag.LatestStockMovements = latestStockMovements
            .GroupBy(movement => movement.ProductId)
            .ToDictionary(group => group.Key, group => group.First());

        ViewData["StockStatusFilter"] = isLowStockFilter ? "low" : null;
        ViewData["Sort"] = normalizedSort;
        ViewData["LowStockProductCount"] = lowStockProducts.Count;
        ViewData["OutOfStockProductCount"] = lowStockProducts.Count(product =>
            StockStatusPresentation.IsOutOfStock(product.StockQuantity, product.MinimumStockQuantity));

        return View(products);
    }

    private static bool IsLowStock(Product product)
    {
        return product.StockQuantity.HasValue
            && product.MinimumStockQuantity.HasValue
            && product.StockQuantity.Value <= product.MinimumStockQuantity.Value;
    }

    private static string? NormalizeSort(string? sort)
    {
        return sort switch
        {
            "valueDesc" => "valueDesc",
            "valueAsc" => "valueAsc",
            "priceDesc" => "priceDesc",
            "priceAsc" => "priceAsc",
            "stockDesc" => "stockDesc",
            "stockAsc" => "stockAsc",
            "marginDesc" => "marginDesc",
            "marginAsc" => "marginAsc",
            _ => null
        };
    }

    private static List<Product> SortProducts(List<Product> products, string? sort)
    {
        // Products whose stock value cannot be computed (missing sale price or stock) always
        // sink to the bottom, regardless of the sort direction. The default (null) sort keeps
        // the existing newest-first ordering established by the query above.
        static decimal? Value(Product product)
            => ProductCostPresentation.StockValueAtSalePrice(product.SalePrice, product.StockQuantity);

        return sort switch
        {
            "valueDesc" => products
                .OrderByDescending(product => Value(product).HasValue)
                .ThenByDescending(product => Value(product) ?? 0m)
                .ToList(),
            "valueAsc" => products
                .OrderByDescending(product => Value(product).HasValue)
                .ThenBy(product => Value(product) ?? 0m)
                .ToList(),
            "priceDesc" => products
                .OrderByDescending(product => product.SalePrice.HasValue)
                .ThenByDescending(product => product.SalePrice ?? 0m)
                .ToList(),
            "priceAsc" => products
                .OrderByDescending(product => product.SalePrice.HasValue)
                .ThenBy(product => product.SalePrice ?? 0m)
                .ToList(),
            "stockDesc" => products
                .OrderByDescending(product => product.StockQuantity.HasValue)
                .ThenByDescending(product => product.StockQuantity ?? 0)
                .ToList(),
            "stockAsc" => products
                .OrderByDescending(product => product.StockQuantity.HasValue)
                .ThenBy(product => product.StockQuantity ?? 0)
                .ToList(),
            "marginDesc" => products
                .OrderByDescending(product => product.TargetMarginPercentage.HasValue)
                .ThenByDescending(product => product.TargetMarginPercentage ?? 0m)
                .ToList(),
            "marginAsc" => products
                .OrderByDescending(product => product.TargetMarginPercentage.HasValue)
                .ThenBy(product => product.TargetMarginPercentage ?? 0m)
                .ToList(),
            _ => products
        };
    }

    public async Task<IActionResult> Details(Guid? id, string? returnTo)
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

        ViewBag.StockMovements = await _context.ProductStockMovements
            .Where(movement => movement.ProductId == product.Id)
            .OrderByDescending(movement => movement.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

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

    public async Task<IActionResult> Edit(Guid? id, string? returnTo)
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

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, Product product, string? returnTo)
    {
        if (id != product.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
            return View(product);
        }

        var existingProduct = await _context.Products.FindAsync(id);

        if (existingProduct is null)
        {
            return NotFound();
        }

        var stockQuantityBeforeUpdate = existingProduct.StockQuantity;

        existingProduct.Name = product.Name;
        existingProduct.Sku = product.Sku;
        existingProduct.Category = product.Category;
        existingProduct.Description = product.Description;
        existingProduct.SalePrice = product.SalePrice;
        existingProduct.StockQuantity = product.StockQuantity;
        existingProduct.MinimumStockQuantity = product.MinimumStockQuantity;
        existingProduct.TargetMarginPercentage = product.TargetMarginPercentage;
        existingProduct.DefaultPackagingCost = product.DefaultPackagingCost;
        existingProduct.IsActive = product.IsActive;

        if (stockQuantityBeforeUpdate != product.StockQuantity)
        {
            var stockBefore = stockQuantityBeforeUpdate ?? 0;
            var stockAfter = product.StockQuantity ?? 0;

            _context.ProductStockMovements.Add(new ProductStockMovement
            {
                ProductId = existingProduct.Id,
                MovementType = StockMovementType.ManualAdjustment,
                QuantityUnits = stockAfter - stockBefore,
                StockBeforeUnits = stockQuantityBeforeUpdate,
                StockAfterUnits = product.StockQuantity,
                Notes = "Ajuste manual realizado no cadastro do produto.",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

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

        var product = await _context.Products.FindAsync(id);

        if (product is null)
        {
            return NotFound();
        }

        var viewModel = new ProductStockAdjustmentViewModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            CurrentStockQuantity = product.StockQuantity
        };

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdjustStock(ProductStockAdjustmentViewModel viewModel, string? returnTo)
    {
        var product = await _context.Products.FindAsync(viewModel.ProductId);

        if (product is null)
        {
            return NotFound();
        }

        var normalizedReturnTo = NormalizeReturnTo(returnTo);

        if (!ModelState.IsValid)
        {
            viewModel.ProductName = product.Name;
            viewModel.CurrentStockQuantity = product.StockQuantity;
            ViewData["ReturnTo"] = normalizedReturnTo;
            return View(viewModel);
        }

        var stockBefore = product.StockQuantity ?? 0;
        int stockAfter;
        int movementQuantity;

        switch (viewModel.AdjustmentType)
        {
            case StockAdjustmentType.Add:
                movementQuantity = viewModel.QuantityUnits;
                stockAfter = stockBefore + viewModel.QuantityUnits;
                break;
            case StockAdjustmentType.Remove:
                movementQuantity = -viewModel.QuantityUnits;
                stockAfter = stockBefore - viewModel.QuantityUnits;
                if (stockAfter < 0)
                {
                    ModelState.AddModelError(nameof(viewModel.QuantityUnits), "A remoção não pode deixar o estoque negativo.");
                    viewModel.ProductName = product.Name;
                    viewModel.CurrentStockQuantity = product.StockQuantity;
                    ViewData["ReturnTo"] = normalizedReturnTo;
                    return View(viewModel);
                }
                break;
            case StockAdjustmentType.Set:
                stockAfter = viewModel.QuantityUnits;
                movementQuantity = stockAfter - stockBefore;
                break;
            default:
                ModelState.AddModelError(nameof(viewModel.AdjustmentType), "Tipo de ajuste inválido.");
                viewModel.ProductName = product.Name;
                viewModel.CurrentStockQuantity = product.StockQuantity;
                ViewData["ReturnTo"] = normalizedReturnTo;
                return View(viewModel);
        }

        if (movementQuantity == 0)
        {
            // A "definir" (Set) adjustment to the current value changes nothing; mirror the
            // Edit flow's guard and skip persisting a misleading zero-quantity ledger entry.
            TempData["SuccessMessage"] = "O estoque informado já é o atual; nenhum ajuste foi registrado.";
            return RedirectToAction(nameof(Details), new { id = product.Id, returnTo = normalizedReturnTo });
        }

        product.StockQuantity = stockAfter;

        _context.ProductStockMovements.Add(new ProductStockMovement
        {
            ProductId = product.Id,
            MovementType = StockMovementType.ManualAdjustment,
            QuantityUnits = movementQuantity,
            StockBeforeUnits = stockBefore,
            StockAfterUnits = stockAfter,
            Notes = string.IsNullOrWhiteSpace(viewModel.Notes)
                ? $"Ajuste manual de estoque ({(movementQuantity > 0 ? "+" : string.Empty)}{movementQuantity} un.)."
                : viewModel.Notes.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Estoque ajustado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = product.Id, returnTo = normalizedReturnTo });
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
    public async Task<IActionResult> Delete(Guid? id, string? returnTo)
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

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

        return View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? returnTo)
    {
        var product = await _context.Products.FindAsync(id);

        if (product is null)
        {
            return NotFound();
        }

        var normalizedReturnTo = NormalizeReturnTo(returnTo);

        var hasPrintJobs = await _context.PrintJobs
            .AnyAsync(printJob => printJob.ProductId == id);

        if (hasPrintJobs)
        {
            TempData["ErrorMessage"] = "Este produto não pode ser removido porque possui produções vinculadas. Desative o produto em vez de removê-lo.";
            return normalizedReturnTo == "lowStock"
                ? RedirectToAction(nameof(Index), new { stockStatus = "low" })
                : RedirectToAction(nameof(Index));
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Produto removido com sucesso.";

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
