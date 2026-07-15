using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using System.Text.Json;
using ThreeDManager.Application.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using ThreeDManager.Application.DTOs;
using ThreeDManager.Web.Presentation;
using ThreeDManager.Web.ViewModels;

namespace ThreeDManager.Web.Controllers;

public class PrintImportsController : Controller
{
    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    private readonly AppDbContext _context;
    private readonly IPrintFileParser _parser;
    private readonly IPrintJobStockService _stockService;

    public PrintImportsController(
        AppDbContext context,
        IPrintFileParser parser,
        IPrintJobStockService stockService)
    {
        _context = context;
        _parser = parser;
        _stockService = stockService;
    }

    public async Task<IActionResult> Index(string? productionState, string? status)
    {
        var linkedPrintJobsByImportId = await _context.PrintJobs
            .Where(printJob => printJob.PrintImportId != null)
            .GroupBy(printJob => printJob.PrintImportId!.Value)
            .Select(group => new
            {
                PrintImportId = group.Key,
                PrintJob = group
                    .OrderByDescending(printJob => printJob.CreatedAt)
                    .Select(printJob => new
                    {
                        printJob.Id,
                        printJob.Status
                    })
                    .First()
            })
            .ToDictionaryAsync(
                link => link.PrintImportId,
                link => (link.PrintJob.Id, link.PrintJob.Status));

        var allImports = await _context.PrintImports
            .OrderByDescending(printImport => printImport.ImportedAt)
            .ToListAsync();

        var processAvailabilityByImportId = allImports.ToDictionary(
            printImport => printImport.Id,
            GetProcessAvailability);

        var pendingImports = allImports
            .Where(printImport => CanGeneratePrintJob(printImport)
                && !linkedPrintJobsByImportId.ContainsKey(printImport.Id))
            .ToList();

        var failedImports = allImports
            .Where(printImport => PrintImportStatus.Normalize(printImport.Status) == PrintImportStatus.Error)
            .ToList();

        var uploadedImports = allImports
            .Where(printImport => PrintImportStatus.Normalize(printImport.Status) == PrintImportStatus.Uploaded)
            .ToList();

        var isPendingProductionFilter = string.Equals(productionState, "pending", StringComparison.OrdinalIgnoreCase);
        var isErrorStatusFilter = string.Equals(status, "error", StringComparison.OrdinalIgnoreCase);
        var isUploadedStatusFilter = string.Equals(status, "uploaded", StringComparison.OrdinalIgnoreCase);

        var imports = isPendingProductionFilter
            ? pendingImports
            : isErrorStatusFilter
                ? failedImports
                : isUploadedStatusFilter
                    ? uploadedImports
                    : allImports;

        ViewData["LinkedPrintJobIdsByImportId"] = linkedPrintJobsByImportId.ToDictionary(
            link => link.Key,
            link => link.Value.Id);
        ViewData["LinkedPrintJobStatusesByImportId"] = linkedPrintJobsByImportId.ToDictionary(
            link => link.Key,
            link => link.Value.Status);
        ViewData["ProcessAvailabilityByImportId"] = processAvailabilityByImportId;
        ViewData["ProductionStateFilter"] = isPendingProductionFilter ? "pending" : null;
        ViewData["PendingProductionImportCount"] = pendingImports.Count;
        ViewData["StatusFilter"] = isErrorStatusFilter ? "error" : isUploadedStatusFilter ? "uploaded" : null;
        ViewData["FailedImportCount"] = failedImports.Count;
        ViewData["UploadedImportCount"] = uploadedImports.Count;

        return View(imports);
    }

    public async Task<IActionResult> Details(Guid? id, string? returnTo)
    {
        if (id is null)
        {
            return NotFound();
        }

        var printImport = await _context.PrintImports
            .FirstOrDefaultAsync(printImport => printImport.Id == id);

        if (printImport is null)
        {
            return NotFound();
        }

        var linkedPrintJob = await FindLinkedPrintJobAsync(printImport.Id);
        ViewData["LinkedPrintJobId"] = linkedPrintJob?.Id;
        ViewData["LinkedPrintJobStatus"] = linkedPrintJob?.Status;
        var processAvailability = GetProcessAvailability(printImport);
        ViewData["CanProcessImport"] = processAvailability.CanProcess;
        ViewData["ProcessImportHint"] = processAvailability.Message;
        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);

        return View(printImport);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(IFormFile? printFile)
    {
        if (printFile is null || printFile.Length == 0)
        {
            ModelState.AddModelError(nameof(printFile), "Selecione um arquivo para importar.");
            return View();
        }

        if (printFile.Length > MaxFileSizeBytes)
        {
            ModelState.AddModelError(nameof(printFile), "O arquivo excede o limite de 20 MB.");
            return View();
        }

        var extension = Path.GetExtension(printFile.FileName).ToLowerInvariant();

        if (extension != ".gcode" && extension != ".g")
        {
            ModelState.AddModelError(nameof(printFile), "Por enquanto, apenas arquivos .gcode ou .g são aceitos.");
            return View();
        }

        string rawContent;

        await using (var stream = printFile.OpenReadStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            rawContent = await reader.ReadToEndAsync();
        }

        var import = new PrintImport
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileName(printFile.FileName),
            FileType = extension.TrimStart('.'),
            RawContent = rawContent,
            ParsedDataJson = null,
            Status = PrintImportStatus.Uploaded,
            ErrorMessage = null,
            ImportedAt = DateTime.UtcNow
        };

        _context.PrintImports.Add(import);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Arquivo importado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = import.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Process(Guid id, string? returnTo)
    {
        var printImport = await _context.PrintImports.FindAsync(id);

        if (printImport is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(printImport.RawContent))
        {
            printImport.Status = PrintImportStatus.Error;
            printImport.ErrorMessage = "A importação não possui conteúdo bruto para processar.";

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "Não foi possível processar: conteúdo bruto vazio.";
            return RedirectAfterProcess(id, returnTo);
        }

        if (!_parser.CanParse(printImport.FileName, printImport.RawContent))
        {
            printImport.Status = PrintImportStatus.Error;
            printImport.ErrorMessage = "Nenhum parser disponível para este arquivo.";

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "Nenhum parser disponível para este arquivo.";
            return RedirectAfterProcess(id, returnTo);
        }

        try
        {
            var parsedMetadata = _parser.Parse(printImport.FileName, printImport.RawContent);

            printImport.ParsedDataJson = JsonSerializer.Serialize(
                parsedMetadata,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            printImport.Status = PrintImportStatus.Parsed;

            printImport.ErrorMessage = parsedMetadata.Warnings.Any()
                ? string.Join(PrintImportMessagePresentation.MessageSeparator, parsedMetadata.Warnings)
                : null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Arquivo processado com sucesso.";
        }
        catch (Exception exception)
        {
            printImport.Status = PrintImportStatus.Error;
            printImport.ErrorMessage = exception.Message;

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "Erro ao processar o arquivo.";
        }

        return RedirectAfterProcess(id, returnTo);
    }

    public async Task<IActionResult> CreatePrintJob(Guid id, string? returnTo)
    {
        var printImport = await _context.PrintImports.FindAsync(id);

        if (printImport is null)
        {
            return NotFound();
        }

        if (!CanGeneratePrintJob(printImport))
        {
            TempData["ErrorMessage"] = "Processе o arquivo com sucesso antes de gerar uma produção.";
            return RedirectToAction(nameof(Details), new { id, returnTo = NormalizeReturnTo(returnTo) });
        }

        var linkedPrintJobId = (await FindLinkedPrintJobAsync(id))?.Id;

        if (linkedPrintJobId is not null)
        {
            TempData["SuccessMessage"] = "Esta importação já possui uma produção vinculada.";
            return RedirectToAction(
                "Details",
                "PrintJobs",
                new
                {
                    id = linkedPrintJobId.Value,
                    returnTo = NormalizeReturnTo(returnTo)
                });
        }

        var metadata = DeserializeParsedMetadata(printImport);

        if (metadata is null)
        {
            TempData["ErrorMessage"] = "Não foi possível ler os dados interpretados da importação.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var viewModel = new PrintJobFromImportViewModel
        {
            ImportId = printImport.Id,
            FileName = printImport.FileName,
            ReturnTo = NormalizeReturnTo(returnTo),
            FilamentUsedGrams = metadata.FilamentUsedGrams,
            FilamentUsedMeters = metadata.FilamentUsedMeters,
            EstimatedTimeMinutes = metadata.EstimatedTimeMinutes,
            ReportedCost = metadata.ReportedCost,
            ParsedMaterialType = metadata.MaterialType,
            Status = PrintJobStatus.Imported
        };

        await PopulatePrintJobOptionsAsync(viewModel, metadata.MaterialType);

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePrintJob(PrintJobFromImportViewModel viewModel)
    {
        var normalizedStatus = PrintJobStatus.Normalize(viewModel.Status);

        if (normalizedStatus is null)
        {
            ModelState.AddModelError(
                nameof(PrintJobFromImportViewModel.Status),
                "Selecione um status de produção válido.");
        }
        else
        {
            viewModel.Status = normalizedStatus;
        }

        if (!ModelState.IsValid)
        {
            await PopulatePrintJobOptionsAsync(viewModel, viewModel.ParsedMaterialType);
            return View(viewModel);
        }

        var printImport = await _context.PrintImports.FindAsync(viewModel.ImportId);

        if (printImport is null)
        {
            return NotFound();
        }

        if (!CanGeneratePrintJob(printImport))
        {
            TempData["ErrorMessage"] = "Processе o arquivo com sucesso antes de gerar uma produção.";
            return RedirectToAction(nameof(Details), new { id = viewModel.ImportId, returnTo = NormalizeReturnTo(viewModel.ReturnTo) });
        }

        var alreadyHasPrintJob = await _context.PrintJobs
            .AnyAsync(printJob => printJob.PrintImportId == viewModel.ImportId);

        if (alreadyHasPrintJob)
        {
            TempData["ErrorMessage"] = "Esta importação já possui uma produção gerada.";
            return RedirectToAction(nameof(Details), new { id = viewModel.ImportId, returnTo = NormalizeReturnTo(viewModel.ReturnTo) });
        }

        var printJob = new PrintJob
        {
            Id = Guid.NewGuid(),
            ProductId = viewModel.ProductId,
            PrinterId = viewModel.PrinterId,
            MaterialId = viewModel.MaterialId,
            PrintImportId = viewModel.ImportId,
            SourceFileName = printImport.FileName,
            FilamentUsedGrams = viewModel.FilamentUsedGrams,
            FilamentUsedMeters = viewModel.FilamentUsedMeters,
            EstimatedTimeMinutes = viewModel.EstimatedTimeMinutes,
            ActualTimeMinutes = viewModel.ActualTimeMinutes,
            ReportedCost = viewModel.ReportedCost,
            PackagingCost = viewModel.PackagingCost,
            UnitsProduced = viewModel.UnitsProduced,
            CalculatedMaterialCost = await CalculateMaterialCostAsync(viewModel.MaterialId, viewModel.FilamentUsedGrams),
            Status = viewModel.Status,
            CreatedAt = DateTime.UtcNow
        };

        var stockResult = await _stockService.ApplyForNewPrintJobAsync(printJob);

        if (!stockResult.Succeeded)
        {
            AddStockModelError(stockResult);
            await PopulatePrintJobOptionsAsync(viewModel, viewModel.ParsedMaterialType);
            return View(viewModel);
        }

        _context.PrintJobs.Add(printJob);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Produção gerada com sucesso.";

        return RedirectToAction(
            "Details",
            "PrintJobs",
            new
            {
                id = printJob.Id,
                returnTo = NormalizeReturnTo(viewModel.ReturnTo)
            });
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
        var fieldName = stockResult.FieldName switch
        {
            nameof(PrintJob.MaterialId) => nameof(PrintJobFromImportViewModel.MaterialId),
            nameof(PrintJob.FilamentUsedGrams) => nameof(PrintJobFromImportViewModel.FilamentUsedGrams),
            _ => string.Empty
        };

        ModelState.AddModelError(fieldName, stockResult.ErrorMessage ?? "Não foi possível atualizar o estoque.");
    }

    private ParsedPrintMetadata? DeserializeParsedMetadata(PrintImport printImport)
    {
        if (string.IsNullOrWhiteSpace(printImport.ParsedDataJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ParsedPrintMetadata>(printImport.ParsedDataJson);
        }
        catch
        {
            return null;
        }
    }

    private static bool CanGeneratePrintJob(PrintImport printImport)
    {
        return PrintImportStatus.Normalize(printImport.Status) == PrintImportStatus.Parsed
            && !string.IsNullOrWhiteSpace(printImport.ParsedDataJson);
    }

    private (bool CanProcess, string? Message) GetProcessAvailability(PrintImport printImport)
    {
        if (string.IsNullOrWhiteSpace(printImport.RawContent))
        {
            return (false, "Esta importação não possui conteúdo bruto salvo. Reimporte o arquivo para tentar novamente.");
        }

        if (!_parser.CanParse(printImport.FileName, printImport.RawContent))
        {
            return (false, "Nenhum parser disponível para este arquivo. Reimporte um arquivo compatível para seguir com a revisão.");
        }

        if (PrintImportStatus.Normalize(printImport.Status) == PrintImportStatus.Error)
        {
            return (true, "O arquivo pode ser processado novamente após revisar o conteúdo bruto abaixo.");
        }

        return (true, null);
    }

    private async Task<(Guid Id, string? Status)?> FindLinkedPrintJobAsync(Guid importId)
    {
        var linkedPrintJob = await _context.PrintJobs
            .Where(printJob => printJob.PrintImportId == importId)
            .OrderByDescending(printJob => printJob.CreatedAt)
            .Select(printJob => new
            {
                printJob.Id,
                printJob.Status
            })
            .FirstOrDefaultAsync();

        return linkedPrintJob is null
            ? null
            : (linkedPrintJob.Id, linkedPrintJob.Status);
    }

    private IActionResult RedirectAfterProcess(Guid id, string? returnTo)
    {
        if (string.Equals(returnTo, "dashboard", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect("/Dashboard");
        }

        if (string.Equals(returnTo, "errorQueue", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { status = "error" });
        }

        if (string.Equals(returnTo, "pendingQueue", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { productionState = "pending" });
        }

        if (string.Equals(returnTo, "uploadedQueue", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { status = "uploaded" });
        }

        return RedirectToAction(nameof(Details), new { id });
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

        if (string.Equals(returnTo, "uploadedQueue", StringComparison.OrdinalIgnoreCase))
        {
            return "uploadedQueue";
        }

        return null;
    }

    private async Task PopulatePrintJobOptionsAsync(PrintJobFromImportViewModel viewModel, string? parsedMaterialType = null)
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

        viewModel.ProductOptions = products.Select(product => new SelectListItem
        {
            Value = product.Id.ToString(),
            Text = string.IsNullOrWhiteSpace(product.Sku)
                ? product.Name
                : $"{product.Name} ({product.Sku})"
        });

        viewModel.PrinterOptions = printers.Select(printer => new SelectListItem
        {
            Value = printer.Id.ToString(),
            Text = $"{printer.Name} - {printer.Brand} {printer.Model}".Trim()
        });

        viewModel.MaterialOptions = materials.Select(material => new SelectListItem
        {
            Value = material.Id.ToString(),
            Text = $"{material.Name} - {material.Type} {material.Color}".Trim()
        });

        viewModel.MaterialCostPerGramById = materials.ToDictionary(
            material => material.Id.ToString(),
            material => material.CostPerKg.HasValue ? material.CostPerKg.Value / 1000m : (decimal?)null);

        viewModel.PrinterCostPerHourById = printers.ToDictionary(
            printer => printer.Id.ToString(),
            printer => printer.CostPerHour);

        viewModel.ProductTargetMarginPercentageById = products.ToDictionary(
            product => product.Id.ToString(),
            product => product.TargetMarginPercentage);

        viewModel.ProductDefaultPackagingCostById = products.ToDictionary(
            product => product.Id.ToString(),
            product => product.DefaultPackagingCost);

        viewModel.StatusOptions = PrintJobStatus.All.Select(status => new SelectListItem
        {
            Value = status,
            Text = PrintJobStatusPresentation.GetDisplayLabel(status)
        });

        if (viewModel.MaterialId is null && !string.IsNullOrWhiteSpace(parsedMaterialType))
        {
            var matchedMaterial = materials.FirstOrDefault(material =>
                string.Equals(material.Type, parsedMaterialType, StringComparison.OrdinalIgnoreCase)
                || material.Name.Contains(parsedMaterialType, StringComparison.OrdinalIgnoreCase));

            if (matchedMaterial is not null)
            {
                viewModel.MaterialId = matchedMaterial.Id;
            }
        }
    }

    public async Task<IActionResult> Delete(Guid? id, string? returnTo)
    {
        if (id is null)
        {
            return NotFound();
        }

        var printImport = await _context.PrintImports
            .FirstOrDefaultAsync(printImport => printImport.Id == id);

        if (printImport is null)
        {
            return NotFound();
        }

        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
        return View(printImport);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id, string? returnTo)
    {
        var printImport = await _context.PrintImports.FindAsync(id);

        if (printImport is null)
        {
            return NotFound();
        }

        var hasPrintJobs = await _context.PrintJobs
            .AnyAsync(printJob => printJob.PrintImportId == id);

        if (hasPrintJobs)
        {
            TempData["ErrorMessage"] = "Esta importação não pode ser removida porque possui produções vinculadas.";
            return RedirectToAction(nameof(Details), new { id, returnTo = NormalizeReturnTo(returnTo) });
        }

        _context.PrintImports.Remove(printImport);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Importação removida com sucesso.";
        return RedirectAfterDelete(returnTo);
    }

    private IActionResult RedirectAfterDelete(string? returnTo)
    {
        var normalizedReturnTo = NormalizeReturnTo(returnTo);

        if (normalizedReturnTo == "errorQueue")
        {
            return RedirectToAction(nameof(Index), new { status = "error" });
        }

        if (normalizedReturnTo == "pendingQueue")
        {
            return RedirectToAction(nameof(Index), new { productionState = "pending" });
        }

        if (normalizedReturnTo == "uploadedQueue")
        {
            return RedirectToAction(nameof(Index), new { status = "uploaded" });
        }

        return RedirectToAction(nameof(Index));
    }
}
