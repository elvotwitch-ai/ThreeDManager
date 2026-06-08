using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using System.Text.Json;
using ThreeDManager.Application.Interfaces;

namespace ThreeDManager.Web.Controllers;

public class PrintImportsController : Controller
{
    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    private readonly AppDbContext _context;
    private readonly IPrintFileParser _parser;

    public PrintImportsController(AppDbContext context, IPrintFileParser parser)
    {
        _context = context;
        _parser = parser;
    }

    public async Task<IActionResult> Index()
    {
        var imports = await _context.PrintImports
            .OrderByDescending(printImport => printImport.ImportedAt)
            .ToListAsync();

        return View(imports);
    }

    public async Task<IActionResult> Details(Guid? id)
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
            Status = "Uploaded",
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
    public async Task<IActionResult> Process(Guid id)
    {
        var printImport = await _context.PrintImports.FindAsync(id);

        if (printImport is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(printImport.RawContent))
        {
            printImport.Status = "Error";
            printImport.ErrorMessage = "A importação não possui conteúdo bruto para processar.";

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "Não foi possível processar: conteúdo bruto vazio.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (!_parser.CanParse(printImport.FileName, printImport.RawContent))
        {
            printImport.Status = "Error";
            printImport.ErrorMessage = "Nenhum parser disponível para este arquivo.";

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "Nenhum parser disponível para este arquivo.";
            return RedirectToAction(nameof(Details), new { id });
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

            printImport.Status = "Parsed";

            printImport.ErrorMessage = parsedMetadata.Warnings.Any()
                ? string.Join(" | ", parsedMetadata.Warnings)
                : null;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Arquivo processado com sucesso.";
        }
        catch (Exception exception)
        {
            printImport.Status = "Error";
            printImport.ErrorMessage = exception.Message;

            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "Erro ao processar o arquivo.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(Guid? id)
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

        return View(printImport);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
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
            return RedirectToAction(nameof(Index));
        }

        _context.PrintImports.Remove(printImport);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Importação removida com sucesso.";
        return RedirectToAction(nameof(Index));
    }
}