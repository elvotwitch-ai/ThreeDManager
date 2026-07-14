using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Web.Presentation;

namespace ThreeDManager.Tests;

// PrintImport.ErrorMessage is overloaded: PrintImportsController.Process stores the parser warnings
// there on a successfully parsed import, and the failure reason on an errored one. These tests pin
// the review surfaces to that distinction, so a parsed-with-warnings import is never presented as a
// failed one.
public class PrintImportReviewIntegrationTests
{
    // Valid g-code (CanParse matches the .gcode extension) that carries none of the metadata
    // comments the parser looks for, so a successful parse still yields every warning.
    private const string GCodeWithoutMetadata = "G28\nG1 X0 Y0 Z0.2 F1500\nG1 X10 Y10 E1\n";

    [Fact]
    public async Task PrintImportDetails_AfterProcessingFileWithMissingMetadata_ShowsWarningsAndNotAnError()
    {
        using var factory = new ThreeDManagerWebFactory();
        var importId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "sem-metadados.gcode",
                FileType = "gcode",
                RawContent = GCodeWithoutMetadata,
                Status = PrintImportStatus.Uploaded,
                ErrorMessage = null,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        // Drive the real Process action so the warnings come from the real parser, not a fixture.
        var detailsBeforeResponse = await client.GetAsync($"/PrintImports/Details/{importId}");
        Assert.Equal(HttpStatusCode.OK, detailsBeforeResponse.StatusCode);
        var token = ExtractAntiForgeryToken(await detailsBeforeResponse.Content.ReadAsStringAsync());

        var processResponse = await client.PostAsync(
            $"/PrintImports/Process/{importId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token
            }));

        Assert.Equal(HttpStatusCode.Redirect, processResponse.StatusCode);

        // The parse succeeded, and the warnings really did land in ErrorMessage.
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var import = await context.PrintImports.SingleAsync(printImport => printImport.Id == importId);

            Assert.Equal(PrintImportStatus.Parsed, import.Status);
            Assert.Equal(4, PrintImportMessagePresentation.SplitMessages(import.ErrorMessage).Count);
        }

        var detailsResponse = await client.GetAsync($"/PrintImports/Details/{importId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);

        // The parsed import presents its parser output as advisory warnings...
        Assert.Contains("data-role=\"import-warnings\"", detailsHtml);
        Assert.Contains("Avisos do processamento", detailsHtml);
        Assert.Contains("Tempo estimado não encontrado no arquivo.", detailsHtml);
        Assert.Contains("Tipo de material não encontrado no arquivo.", detailsHtml);
        Assert.Contains("O arquivo foi processado com sucesso.", detailsHtml);

        // ...and never as a failure. This is the regression the batch fixes: before, the joined
        // warnings rendered under a red "Erro" label while the status badge read "Processado".
        Assert.DoesNotContain("data-role=\"import-error\"", detailsHtml);
        Assert.DoesNotContain("text-danger", detailsHtml);
        Assert.DoesNotContain("Nenhum erro registrado.", detailsHtml);
    }

    [Fact]
    public async Task PrintImportDetails_WhenImportFailed_StillShowsTheMessageAsAnError()
    {
        using var factory = new ThreeDManagerWebFactory();
        var importId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "falhou.gcode",
                FileType = "gcode",
                RawContent = GCodeWithoutMetadata,
                Status = PrintImportStatus.Error,
                ErrorMessage = "Nenhum parser disponível para este arquivo.",
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintImports/Details/{importId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("data-role=\"import-error\"", detailsHtml);
        Assert.Contains("Nenhum parser disponível para este arquivo.", detailsHtml);
        Assert.DoesNotContain("data-role=\"import-warnings\"", detailsHtml);
        Assert.DoesNotContain("Avisos do processamento", detailsHtml);
    }

    [Fact]
    public async Task PrintImportDetails_WhenParsedWithoutWarnings_ShowsNoErrorAndNoWarnings()
    {
        using var factory = new ThreeDManagerWebFactory();
        var importId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "completo.gcode",
                FileType = "gcode",
                RawContent = GCodeWithoutMetadata,
                ParsedDataJson = "{}",
                Status = PrintImportStatus.Parsed,
                ErrorMessage = null,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintImports/Details/{importId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Nenhum erro registrado.", detailsHtml);
        Assert.DoesNotContain("data-role=\"import-warnings\"", detailsHtml);
        Assert.DoesNotContain("data-role=\"import-error\"", detailsHtml);
    }

    [Fact]
    public async Task PrintImportsIndex_FlagsParsedImportsCarryingParserWarnings()
    {
        using var factory = new ThreeDManagerWebFactory();
        var warnedImportId = Guid.NewGuid();
        var cleanImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = warnedImportId,
                    FileName = "com-avisos.gcode",
                    FileType = "gcode",
                    RawContent = GCodeWithoutMetadata,
                    ParsedDataJson = "{}",
                    Status = PrintImportStatus.Parsed,
                    ErrorMessage = string.Join(
                        PrintImportMessagePresentation.MessageSeparator,
                        "Tempo estimado não encontrado no arquivo.",
                        "Tipo de material não encontrado no arquivo."),
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = cleanImportId,
                    FileName = "sem-avisos.gcode",
                    FileType = "gcode",
                    RawContent = GCodeWithoutMetadata,
                    ParsedDataJson = "{}",
                    Status = PrintImportStatus.Parsed,
                    ErrorMessage = null,
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/PrintImports");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("data-role=\"import-warning-note\"", indexHtml);
        Assert.Contains("2 avisos do processamento.", indexHtml);

        // The clean import is on the same page, so exactly one row may carry the note.
        Assert.Contains("sem-avisos.gcode", indexHtml);
        Assert.Single(Regex.Matches(indexHtml, "data-role=\"import-warning-note\""));
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Could not find antiforgery token in form html.");
        return match.Groups[1].Value;
    }
}
