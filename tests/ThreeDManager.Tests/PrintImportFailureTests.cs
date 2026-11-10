using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ThreeDManager.Application.DTOs;
using ThreeDManager.Application.Interfaces;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Infrastructure.Parsers;
using ThreeDManager.Web.Controllers;

namespace ThreeDManager.Tests;

public class PrintImportFailureTests
{
    private const string FriendlyError = "Não foi possível ler o arquivo. Verifique o conteúdo e importe novamente.";

    [Fact]
    public async Task Process_InvalidDuration_PersistsFriendlyErrorWithoutInternalDetails()
    {
        using var context = CreateContext();
        var import = NewImport();
        import.RawContent = "; estimated printing time = 79228162514264337593543950335h";
        import.ParsedDataJson = "{\"old\":true}";
        context.Add(import);
        await context.SaveChangesAsync();
        var controller = CreateController(context, new GCodePrintFileParser());

        Assert.IsType<RedirectToActionResult>(await controller.Process(import.Id, null));
        context.ChangeTracker.Clear();
        var saved = await context.PrintImports.SingleAsync();
        Assert.Equal(PrintImportStatus.Error, saved.Status);
        Assert.Equal(FriendlyError, saved.ErrorMessage);
        Assert.Null(saved.ParsedDataJson);
        Assert.False(controller.TempData.ContainsKey("SuccessMessage"));
    }

    [Fact]
    public async Task Process_SaveFailsOnce_PropagatesWithoutRetryOrFileError()
    {
        var failure = new FailNextSaveInterceptor();
        using var context = CreateContext(failure);
        var import = NewImport();
        context.Add(import);
        await context.SaveChangesAsync();
        failure.Armed = true;
        var controller = CreateController(context, new GCodePrintFileParser());

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => controller.Process(import.Id, null));
        Assert.Same(failure.Error, exception);
        Assert.Equal(1, failure.Attempts);
        Assert.Equal(PrintImportStatus.Parsed, import.Status);
        Assert.NotEqual(PrintImportStatus.Error, import.Status);
        Assert.DoesNotContain(failure.Error.Message, import.ErrorMessage ?? string.Empty);
        Assert.Empty(controller.TempData);
        context.ChangeTracker.Clear();
        Assert.Equal(PrintImportStatus.Uploaded, (await context.PrintImports.SingleAsync()).Status);
    }

    [Fact]
    public async Task CreatePrintJob_InvalidJson_ShowsFriendlyError()
    {
        using var context = CreateContext();
        var import = NewImport();
        import.Status = PrintImportStatus.Parsed;
        import.ParsedDataJson = "{invalid";
        context.Add(import);
        await context.SaveChangesAsync();
        var controller = CreateController(context, new GCodePrintFileParser());
        Assert.IsType<RedirectToActionResult>(await controller.CreatePrintJob(import.Id, null));
        Assert.Equal("Não foi possível ler os dados interpretados da importação.", controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public void DeserializeParsedMetadata_OnlyCatchesInvalidJson_NotUnexpectedRuntimeErrors()
    {
        // Guard the compiled exception boundary as ordinary DTO JSON cannot inject a
        // non-JSON runtime failure. Functional invalid-JSON behavior is covered separately.
        var method = typeof(PrintImportsController).GetMethod("DeserializeParsedMetadata",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var clause = Assert.Single(method.GetMethodBody()!.ExceptionHandlingClauses);
        Assert.Equal(typeof(System.Text.Json.JsonException), clause.CatchType);
    }

    private sealed class FailNextSaveInterceptor : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public int Attempts { get; private set; }
        public DbUpdateException Error { get; } = new("Synthetic persistence failure");
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && ++Attempts == 1)
            {
                throw Error;
            }
            return new(result);
        }
    }

    private static AppDbContext CreateContext(params IInterceptor[] interceptors) => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptors).Options);

    private static PrintImport NewImport() => new()
    {
        Id = Guid.NewGuid(),
        FileName = "synthetic.gcode",
        FileType = "gcode",
        RawContent = "G28",
        Status = PrintImportStatus.Uploaded,
        ImportedAt = DateTime.UtcNow
    };

    private static PrintImportsController CreateController(AppDbContext context, IPrintFileParser parser)
    {
        var controller = new PrintImportsController(context, parser, null!);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new EmptyTempDataProvider());
        return controller;
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
