using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;

namespace ThreeDManager.Tests;

public class PrintJobControllerIntegrationTests
{
    [Fact]
    public async Task CreatePrintJob_FromImport_DeductsStockAndCreatesCompletedJob()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "sample.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ImportId"] = importId.ToString(),
            ["FileName"] = "sample.gcode",
            ["ParsedMaterialType"] = "PLA",
            ["ProductId"] = ids.ProductId.ToString(),
            ["PrinterId"] = ids.PrinterId.ToString(),
            ["MaterialId"] = ids.MaterialId.ToString(),
            ["FilamentUsedGrams"] = "12.45",
            ["FilamentUsedMeters"] = "1.23",
            ["EstimatedTimeMinutes"] = "60",
            ["ActualTimeMinutes"] = "",
            ["ReportedCost"] = "2.50",
            ["Status"] = "Completed"
        };

        var postResponse = await client.PostAsync(
            "/PrintImports/CreatePrintJob",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/PrintJobs/Details/{await GetCreatedPrintJobIdAsync(factory, importId)}", postResponse.Headers.Location?.ToString());

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.PrintImportId == importId);

        Assert.Equal(987.55m, material.CurrentStockGrams);
        Assert.Equal("Completed", printJob.Status);
        Assert.Equal(12.45m, printJob.StockDeductedGrams);
        Assert.Equal(material.Id, printJob.StockDeductedMaterialId);

        using var detailsClient = factory.CreateTestClient();
        var detailsResponse = await detailsClient.GetAsync($"/PrintJobs/Details/{printJob.Id}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Movimentação de estoque vinculada", detailsHtml);
        Assert.Contains("Baixa automática", detailsHtml);
        Assert.Contains("Baixa automática de estoque", detailsHtml);
    }

    [Fact]
    public async Task CreatePrintJob_FromImport_ShowsValidation_WhenStockIsInsufficient()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory, stockGrams: 10m);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "sample.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var response = await client.PostAsync(
            "/PrintImports/CreatePrintJob",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ImportId"] = importId.ToString(),
                ["FileName"] = "sample.gcode",
                ["ParsedMaterialType"] = "PLA",
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Completed"
            }));

        var responseHtml = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Estoque insuficiente para concluir esta produção.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
        var hasPrintJobForImport = await context.PrintJobs.AnyAsync(printJob => printJob.PrintImportId == importId);

        Assert.Equal(10m, material.CurrentStockGrams);
        Assert.False(hasPrintJobForImport);
    }

    [Fact]
    public async Task CreatePrintJob_FromImport_RejectsUnknownStatus()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "sample.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            "/PrintImports/CreatePrintJob",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ImportId"] = importId.ToString(),
                ["FileName"] = "sample.gcode",
                ["ParsedMaterialType"] = "PLA",
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Done"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Selecione um status de produção válido.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasPrintJobForImport = await context.PrintJobs.AnyAsync(printJob => printJob.PrintImportId == importId);
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);

        Assert.False(hasPrintJobForImport);
        Assert.Equal(1000m, material.CurrentStockGrams);
    }

    [Fact]
    public async Task PrintImportStatus_NormalizesStoredStatusCasing_OnImportAndDashboardViews()
    {
        using var factory = new ThreeDManagerWebFactory();

        var parsedImportId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = parsedImportId,
                    FileName = "parsed-lower.gcode",
                    FileType = "gcode",
                    RawContent = "; filament used [g] = 12.45",
                    ParsedDataJson = """
                    {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                    """,
                    Status = "parsed",
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = Guid.NewGuid(),
                    FileName = "error-lower.gcode",
                    FileType = "gcode",
                    RawContent = string.Empty,
                    Status = "error",
                    ErrorMessage = "Falha de teste",
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/PrintImports");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Processado", indexHtml);
        Assert.Contains("Erro", indexHtml);

        var detailsResponse = await client.GetAsync($"/PrintImports/Details/{parsedImportId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Gerar produção", detailsHtml);

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("error-lower.gcode", dashboardHtml);
        Assert.Contains("Falha de teste", dashboardHtml);
    }

    [Fact]
    public async Task PrintImportsViews_ShowLocalizedImportStatusLabels_InIndexDetailsAndDelete()
    {
        using var factory = new ThreeDManagerWebFactory();

        var uploadedImportId = Guid.NewGuid();
        var parsedImportId = Guid.NewGuid();
        var errorImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = uploadedImportId,
                    FileName = "uploaded.gcode",
                    FileType = "gcode",
                    RawContent = "; uploaded only",
                    Status = PrintImportStatus.Uploaded,
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = parsedImportId,
                    FileName = "parsed.gcode",
                    FileType = "gcode",
                    RawContent = "; parsed",
                    ParsedDataJson = """
                    {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                    """,
                    Status = PrintImportStatus.Parsed,
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                },
                new PrintImport
                {
                    Id = errorImportId,
                    FileName = "error.gcode",
                    FileType = "gcode",
                    RawContent = string.Empty,
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Falha de teste",
                    ImportedAt = DateTime.UtcNow.AddMinutes(-2)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/PrintImports");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Importado", indexHtml);
        Assert.Contains("Processado", indexHtml);
        Assert.Contains("Erro", indexHtml);
        Assert.DoesNotContain(">Uploaded<", indexHtml);
        Assert.DoesNotContain(">Parsed<", indexHtml);
        Assert.DoesNotContain(">Error<", indexHtml);

        var detailsResponse = await client.GetAsync($"/PrintImports/Details/{parsedImportId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("badge bg-success", detailsHtml);
        Assert.Contains("Processado", detailsHtml);
        Assert.DoesNotContain(">Parsed<", detailsHtml);

        var deleteResponse = await client.GetAsync($"/PrintImports/Delete/{errorImportId}");
        var deleteHtml = WebUtility.HtmlDecode(await deleteResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Contains("badge bg-danger", deleteHtml);
        Assert.Contains("Erro", deleteHtml);
        Assert.DoesNotContain(">Error<", deleteHtml);
    }

    [Fact]
    public async Task CreatePrintJob_FromImport_RejectsErrorImport_EvenWithParsedData()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "failed-with-stale-data.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Error,
                ErrorMessage = "Falha de parser",
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}");

        Assert.Equal(HttpStatusCode.Redirect, getResponse.StatusCode);
        Assert.Equal($"/PrintImports/Details/{importId}", getResponse.Headers.Location?.ToString());

        var createPageResponse = await client.GetAsync("/PrintImports/Create");
        Assert.Equal(HttpStatusCode.OK, createPageResponse.StatusCode);
        var token = ExtractAntiForgeryToken(await createPageResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/PrintImports/CreatePrintJob",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ImportId"] = importId.ToString(),
                ["FileName"] = "failed-with-stale-data.gcode",
                ["ParsedMaterialType"] = "PLA",
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Completed"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/PrintImports/Details/{importId}", postResponse.Headers.Location?.ToString());

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasPrintJobForImport = await context.PrintJobs.AnyAsync(printJob => printJob.PrintImportId == importId);
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);

        Assert.False(hasPrintJobForImport);
        Assert.Equal(1000m, material.CurrentStockGrams);
    }

    [Fact]
    public async Task PrintImportDetails_ShowsRetryGuidance_OnlyWhenErrorImportCanBeProcessedAgain()
    {
        using var factory = new ThreeDManagerWebFactory();

        var retryableImportId = Guid.NewGuid();
        var blockedImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = retryableImportId,
                    FileName = "retryable-error.gcode",
                    FileType = "gcode",
                    RawContent = "; generated by Creality Print\n; filament used [g] = 12.45\n;TIME:3600\nG28",
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Falha transitória de teste",
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = blockedImportId,
                    FileName = "blocked-error.gcode",
                    FileType = "gcode",
                    RawContent = string.Empty,
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Conteúdo ausente",
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var retryableResponse = await client.GetAsync($"/PrintImports/Details/{retryableImportId}");
        var retryableHtml = WebUtility.HtmlDecode(await retryableResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, retryableResponse.StatusCode);
        Assert.Contains("Tentar processar novamente", retryableHtml);
        Assert.Contains("O arquivo pode ser processado novamente após revisar o conteúdo bruto abaixo.", retryableHtml);

        var blockedResponse = await client.GetAsync($"/PrintImports/Details/{blockedImportId}");
        var blockedHtml = WebUtility.HtmlDecode(await blockedResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, blockedResponse.StatusCode);
        Assert.DoesNotContain("Tentar processar novamente", blockedHtml);
        Assert.DoesNotContain("Processar arquivo", blockedHtml);
        Assert.Contains("Esta importação não possui conteúdo bruto salvo. Reimporte o arquivo para tentar novamente.", blockedHtml);
    }

    [Fact]
    public async Task PrintImportDetails_LinksExistingPrintJob_WhenImportAlreadyGeneratedProduction()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "already-linked.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                PrintImportId = importId,
                SourceFileName = "already-linked.gcode",
                FilamentUsedGrams = 12.45m,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var detailsResponse = await client.GetAsync($"/PrintImports/Details/{importId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Ver produção vinculada", detailsHtml);
        Assert.Contains("Produção vinculada", detailsHtml);
        Assert.Contains("Concluída", detailsHtml);
        Assert.DoesNotContain(">Completed<", detailsHtml);
        Assert.Contains($"/PrintJobs/Details/{printJobId}", detailsHtml);
        Assert.DoesNotContain("Gerar produção", detailsHtml);

        var createResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}");
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal($"/PrintJobs/Details/{printJobId}", createResponse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task PrintImportsIndex_ShowsLocalizedLinkedProductionStatus()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var linkedImportId = Guid.NewGuid();
        var linkedPrintJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = linkedImportId,
                FileName = "linked-status.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            context.PrintJobs.Add(new PrintJob
            {
                Id = linkedPrintJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                PrintImportId = linkedImportId,
                SourceFileName = "linked-status.gcode",
                Status = PrintJobStatus.Canceled,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var response = await client.GetAsync("/PrintImports");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("linked-status.gcode", html);
        Assert.Contains("Vinculada", html);
        Assert.Contains("Cancelada", html);
        Assert.DoesNotContain(">Canceled<", html);
        Assert.Contains($"/PrintJobs/Details/{linkedPrintJobId}", html);
    }

    [Fact]
    public async Task CreatePrintJob_PreservesPendingQueueContext_WhenImportAlreadyLinkedToProduction()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "already-linked-pending.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                PrintImportId = importId,
                SourceFileName = "already-linked-pending.gcode",
                FilamentUsedGrams = 12.45m,
                Status = PrintJobStatus.Imported,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var createResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}?returnTo=pendingQueue");
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Equal($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue", createResponse.Headers.Location?.ToString());

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("/PrintImports?productionState=pending", detailsHtml);
        Assert.Contains($"/PrintImports/Details/{importId}?returnTo=pendingQueue", detailsHtml);
        Assert.Contains("Voltar para pendentes", detailsHtml);
    }

    [Fact]
    public async Task PrintImportsIndex_ShowsRecoveryHint_AndHidesUnsupportedProcessAction_ForErrorImports()
    {
        using var factory = new ThreeDManagerWebFactory();
        await SeedCommonAsync(factory);

        var retryableImportId = Guid.NewGuid();
        var blockedImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = retryableImportId,
                    FileName = "retryable-error-index.gcode",
                    FileType = "gcode",
                    RawContent = ";FLAVOR:Marlin\n;Generated by Creality Print\n;Filament used: 1.23m, 12.45g\n;TIME:3600\n",
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Falha transitória de teste",
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = blockedImportId,
                    FileName = "blocked-error-index.gcode",
                    FileType = "gcode",
                    RawContent = string.Empty,
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Conteúdo ausente",
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var indexResponse = await client.GetAsync("/PrintImports");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("retryable-error-index.gcode", indexHtml);
        Assert.Contains("blocked-error-index.gcode", indexHtml);
        Assert.Contains("Pode tentar processar novamente.", indexHtml);
        Assert.Contains("Reimporte um arquivo compatível para seguir.", indexHtml);
        Assert.Contains($"/PrintImports/Process/{retryableImportId}", indexHtml);
        Assert.DoesNotContain($"/PrintImports/Process/{blockedImportId}", indexHtml);
        Assert.Contains("Tentar novamente", indexHtml);
    }

    [Fact]
    public async Task PrintImportsIndex_FiltersFailedImports_ForRecoveryReview()
    {
        using var factory = new ThreeDManagerWebFactory();
        await SeedCommonAsync(factory);

        var retryableImportId = Guid.NewGuid();
        var blockedImportId = Guid.NewGuid();
        var pendingImportId = Guid.NewGuid();
        var uploadedImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = retryableImportId,
                    FileName = "retryable-filter-error.gcode",
                    FileType = "gcode",
                    RawContent = ";FLAVOR:Marlin\n;Generated by Creality Print\n;Filament used: 1.23m, 12.45g\n;TIME:3600\n",
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Falha transitória de teste",
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = blockedImportId,
                    FileName = "blocked-filter-error.gcode",
                    FileType = "gcode",
                    RawContent = string.Empty,
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Conteúdo ausente",
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                },
                new PrintImport
                {
                    Id = pendingImportId,
                    FileName = "pending-filter.gcode",
                    FileType = "gcode",
                    ParsedDataJson = """
                    {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                    """,
                    Status = PrintImportStatus.Parsed,
                    ImportedAt = DateTime.UtcNow.AddMinutes(-2)
                },
                new PrintImport
                {
                    Id = uploadedImportId,
                    FileName = "uploaded-filter.gcode",
                    FileType = "gcode",
                    RawContent = "; aguardando processamento",
                    Status = PrintImportStatus.Uploaded,
                    ImportedAt = DateTime.UtcNow.AddMinutes(-3)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var failedResponse = await client.GetAsync("/PrintImports?status=error");
        var failedHtml = WebUtility.HtmlDecode(await failedResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, failedResponse.StatusCode);
        Assert.Contains("Falhas (", failedHtml);
        Assert.Contains("retryable-filter-error.gcode", failedHtml);
        Assert.Contains("blocked-filter-error.gcode", failedHtml);
        Assert.DoesNotContain("pending-filter.gcode", failedHtml);
        Assert.DoesNotContain("uploaded-filter.gcode", failedHtml);
        Assert.Contains("Pode tentar processar novamente.", failedHtml);
        Assert.Contains("Reimporte um arquivo compatível para seguir.", failedHtml);
    }

    [Fact]
    public async Task PrintImportsIndex_RetainsFailedFilterAndShowsRetryResult()
    {
        using var factory = new ThreeDManagerWebFactory();
        await SeedCommonAsync(factory);

        var retryableImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = retryableImportId,
                FileName = "retry-from-error-queue.gcode",
                FileType = "gcode",
                RawContent = ";FLAVOR:Marlin\n;Generated by Creality Print\n;Filament used: 1.23m, 12.45g\n;TIME:3600\n",
                Status = PrintImportStatus.Error,
                ErrorMessage = "Falha transitória de teste",
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var failedResponse = await client.GetAsync("/PrintImports?status=error");
        var failedHtml = WebUtility.HtmlDecode(await failedResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, failedResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"errorQueue\"", failedHtml);

        var token = ExtractAntiForgeryToken(failedHtml);
        var retryResponse = await client.PostAsync(
            $"/PrintImports/Process/{retryableImportId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["returnTo"] = "errorQueue"
            }));

        Assert.Equal(HttpStatusCode.Redirect, retryResponse.StatusCode);
        Assert.Equal("/PrintImports?status=error", retryResponse.Headers.Location?.ToString());

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var retryableImport = await context.PrintImports.SingleAsync(printImport => printImport.Id == retryableImportId);

        Assert.Equal(PrintImportStatus.Parsed, retryableImport.Status);
        Assert.False(string.IsNullOrWhiteSpace(retryableImport.ParsedDataJson));

        var refreshedQueueResponse = await client.GetAsync("/PrintImports?status=error");
        var refreshedQueueHtml = WebUtility.HtmlDecode(await refreshedQueueResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshedQueueResponse.StatusCode);
        Assert.Contains("Arquivo processado com sucesso.", refreshedQueueHtml);
        Assert.DoesNotContain("retry-from-error-queue.gcode", refreshedQueueHtml);
    }

    [Fact]
    public async Task DeletePrintImport_PreservesFailedQueueContext_OnGetCancelAndSuccessfulPost()
    {
        using var factory = new ThreeDManagerWebFactory();

        var failedImportId = Guid.NewGuid();
        var otherFailedImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = failedImportId,
                    FileName = "failed-delete-context.gcode",
                    FileType = "gcode",
                    RawContent = string.Empty,
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Falha para exclusão",
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = otherFailedImportId,
                    FileName = "failed-still-listed.gcode",
                    FileType = "gcode",
                    RawContent = string.Empty,
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Outra falha",
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var failedQueueResponse = await client.GetAsync("/PrintImports?status=error");
        var failedQueueHtml = WebUtility.HtmlDecode(await failedQueueResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, failedQueueResponse.StatusCode);
        Assert.Contains($"/PrintImports/Delete/{failedImportId}?returnTo=errorQueue", failedQueueHtml);

        var deleteResponse = await client.GetAsync($"/PrintImports/Delete/{failedImportId}?returnTo=errorQueue");
        var deleteHtml = WebUtility.HtmlDecode(await deleteResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"errorQueue\"", deleteHtml);
        Assert.Contains($"/PrintImports/Details/{failedImportId}?returnTo=errorQueue", deleteHtml);

        var token = ExtractAntiForgeryToken(deleteHtml);
        var postResponse = await client.PostAsync(
            $"/PrintImports/Delete/{failedImportId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = failedImportId.ToString(),
                ["returnTo"] = "errorQueue"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal("/PrintImports?status=error", postResponse.Headers.Location?.ToString());

        var refreshedQueueResponse = await client.GetAsync("/PrintImports?status=error");
        var refreshedQueueHtml = WebUtility.HtmlDecode(await refreshedQueueResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, refreshedQueueResponse.StatusCode);
        Assert.DoesNotContain("failed-delete-context.gcode", refreshedQueueHtml);
        Assert.Contains("failed-still-listed.gcode", refreshedQueueHtml);
        Assert.Contains("Importação removida com sucesso.", refreshedQueueHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedImport = await context.PrintImports.SingleOrDefaultAsync(printImport => printImport.Id == failedImportId);
        var remainingImport = await context.PrintImports.SingleOrDefaultAsync(printImport => printImport.Id == otherFailedImportId);

        Assert.Null(deletedImport);
        Assert.NotNull(remainingImport);
    }

    [Fact]
    public async Task PrintImportDetails_PreservesFilteredQueueForBackAndRetryActions()
    {
        using var factory = new ThreeDManagerWebFactory();
        await SeedCommonAsync(factory);

        var retryableImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = retryableImportId,
                FileName = "retry-from-filtered-details.gcode",
                FileType = "gcode",
                RawContent = ";FLAVOR:Marlin\n;Generated by Creality Print\n;Filament used: 1.23m, 12.45g\n;TIME:3600\n",
                Status = PrintImportStatus.Error,
                ErrorMessage = "Falha transitória de teste",
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var failedResponse = await client.GetAsync("/PrintImports?status=error");
        var failedHtml = WebUtility.HtmlDecode(await failedResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, failedResponse.StatusCode);
        Assert.Contains($"/PrintImports/Details/{retryableImportId}?returnTo=errorQueue", failedHtml);

        var detailsResponse = await client.GetAsync($"/PrintImports/Details/{retryableImportId}?returnTo=errorQueue");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"errorQueue\"", detailsHtml);
        Assert.Contains("/PrintImports?status=error", detailsHtml);
        Assert.Contains("Voltar para falhas", detailsHtml);

        var token = ExtractAntiForgeryToken(detailsHtml);
        var retryResponse = await client.PostAsync(
            $"/PrintImports/Process/{retryableImportId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["returnTo"] = "errorQueue"
            }));

        Assert.Equal(HttpStatusCode.Redirect, retryResponse.StatusCode);
        Assert.Equal("/PrintImports?status=error", retryResponse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task CreatePrintJob_PreservesPendingQueueContext_OnGetAndValidationError()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var pendingImportId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = pendingImportId,
                FileName = "pending-create-context.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var pendingResponse = await client.GetAsync("/PrintImports?productionState=pending");
        var pendingHtml = WebUtility.HtmlDecode(await pendingResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        Assert.Contains($"/PrintImports/CreatePrintJob/{pendingImportId}?returnTo=pendingQueue", pendingHtml);

        var createResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{pendingImportId}?returnTo=pendingQueue");
        var createHtml = WebUtility.HtmlDecode(await createResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Contains("name=\"ReturnTo\" value=\"pendingQueue\"", createHtml);
        Assert.Contains($"/PrintImports/Details/{pendingImportId}?returnTo=pendingQueue", createHtml);
        Assert.Contains("/PrintImports?productionState=pending", createHtml);
        Assert.Contains("Voltar para pendentes", createHtml);

        var token = ExtractAntiForgeryToken(createHtml);
        var validationResponse = await client.PostAsync(
            "/PrintImports/CreatePrintJob",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ImportId"] = pendingImportId.ToString(),
                ["FileName"] = "pending-create-context.gcode",
                ["ReturnTo"] = "pendingQueue",
                ["ParsedMaterialType"] = "PLA",
                ["ProductId"] = string.Empty,
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = string.Empty,
                ["ReportedCost"] = "2.50",
                ["Status"] = "Completed"
            }));

        var validationHtml = WebUtility.HtmlDecode(await validationResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, validationResponse.StatusCode);
        Assert.Contains("Selecione um produto.", validationHtml);
        Assert.Contains("name=\"ReturnTo\" value=\"pendingQueue\"", validationHtml);
        Assert.Contains($"/PrintImports/Details/{pendingImportId}?returnTo=pendingQueue", validationHtml);
        Assert.Contains("/PrintImports?productionState=pending", validationHtml);
        Assert.Contains("Voltar para pendentes", validationHtml);
    }

    [Fact]
    public async Task CreatePrintJob_PreservesPendingQueueContext_AfterSuccessfulPost()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var pendingImportId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = pendingImportId,
                FileName = "pending-create-success.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var createResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{pendingImportId}?returnTo=pendingQueue");
        var createHtml = WebUtility.HtmlDecode(await createResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        Assert.Contains("name=\"ReturnTo\" value=\"pendingQueue\"", createHtml);

        var token = ExtractAntiForgeryToken(createHtml);
        var postResponse = await client.PostAsync(
            "/PrintImports/CreatePrintJob",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ImportId"] = pendingImportId.ToString(),
                ["FileName"] = "pending-create-success.gcode",
                ["ReturnTo"] = "pendingQueue",
                ["ParsedMaterialType"] = "PLA",
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = string.Empty,
                ["ReportedCost"] = "2.50",
                ["Status"] = "Completed"
            }));

        var printJobId = await GetCreatedPrintJobIdAsync(factory, pendingImportId);

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue", postResponse.Headers.Location?.ToString());

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("/PrintImports?productionState=pending", detailsHtml);
        Assert.Contains($"/PrintImports/Details/{pendingImportId}?returnTo=pendingQueue", detailsHtml);
        Assert.Contains("Voltar para pendentes", detailsHtml);
    }

    [Fact]
    public async Task EditPrintJob_PreservesPendingQueueContext_OnGetAndSuccessfulPost()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var pendingImportId = Guid.NewGuid();
        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = pendingImportId,
                FileName = "pending-edit-context.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                PrintImportId = pendingImportId,
                SourceFileName = "pending-edit-context.gcode",
                FilamentUsedGrams = 12.45m,
                FilamentUsedMeters = 1.23m,
                EstimatedTimeMinutes = 60,
                ReportedCost = 2.50m,
                Status = PrintJobStatus.Imported,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains($"/PrintJobs/Edit/{printJobId}?returnTo=pendingQueue", detailsHtml);

        var editResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}?returnTo=pendingQueue");
        var editHtml = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"pendingQueue\"", editHtml);
        Assert.Contains($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue", editHtml);

        var token = ExtractAntiForgeryToken(editHtml);
        var postResponse = await client.PostAsync(
            $"/PrintJobs/Edit/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = printJobId.ToString(),
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["PrintImportId"] = pendingImportId.ToString(),
                ["SourceFileName"] = "pending-edit-context.gcode",
                ["CreatedAt"] = "2026-06-24T12:00:00Z",
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "75",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Planned",
                ["returnTo"] = "pendingQueue"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue", postResponse.Headers.Location?.ToString());

        var refreshedDetailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue");
        var refreshedDetailsHtml = WebUtility.HtmlDecode(await refreshedDetailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, refreshedDetailsResponse.StatusCode);
        Assert.Contains("/PrintImports?productionState=pending", refreshedDetailsHtml);
        Assert.Contains($"/PrintImports/Details/{pendingImportId}?returnTo=pendingQueue", refreshedDetailsHtml);
        Assert.Contains("Voltar para pendentes", refreshedDetailsHtml);
    }

    [Fact]
    public async Task EditPrintJob_PreservesPendingQueueContext_OnStockValidationError()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var pendingImportId = Guid.NewGuid();
        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = pendingImportId,
                FileName = "pending-edit-validation.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                PrintImportId = pendingImportId,
                SourceFileName = "pending-edit-validation.gcode",
                FilamentUsedGrams = 12.45m,
                FilamentUsedMeters = 1.23m,
                EstimatedTimeMinutes = 60,
                ReportedCost = 2.50m,
                Status = PrintJobStatus.Imported,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var editResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}?returnTo=pendingQueue");
        var editHtml = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

        var token = ExtractAntiForgeryToken(editHtml);
        var postResponse = await client.PostAsync(
            $"/PrintJobs/Edit/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = printJobId.ToString(),
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["PrintImportId"] = pendingImportId.ToString(),
                ["SourceFileName"] = "pending-edit-validation.gcode",
                ["CreatedAt"] = "2026-06-24T12:00:00Z",
                ["FilamentUsedGrams"] = "1200.00",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "75",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Completed",
                ["returnTo"] = "pendingQueue"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Estoque insuficiente para concluir esta produção.", responseHtml);
        Assert.Contains("name=\"returnTo\" value=\"pendingQueue\"", responseHtml);
        Assert.Contains($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.Id == printJobId);

        Assert.Equal(1000m, material.CurrentStockGrams);
        Assert.Equal(PrintJobStatus.Imported, printJob.Status);
        Assert.Null(printJob.StockDeductedAt);
        Assert.Null(printJob.StockDeductedGrams);
    }

    [Fact]
    public async Task DeletePrintJob_PreservesPendingQueueContext_OnGetCancelAndSuccessfulPost()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var pendingImportId = Guid.NewGuid();
        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = pendingImportId,
                FileName = "pending-delete-context.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                PrintImportId = pendingImportId,
                SourceFileName = "pending-delete-context.gcode",
                FilamentUsedGrams = 12.45m,
                FilamentUsedMeters = 1.23m,
                EstimatedTimeMinutes = 60,
                ReportedCost = 2.50m,
                Status = PrintJobStatus.Planned,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains($"/PrintJobs/Delete/{printJobId}?returnTo=pendingQueue", detailsHtml);

        var deleteResponse = await client.GetAsync($"/PrintJobs/Delete/{printJobId}?returnTo=pendingQueue");
        var deleteHtml = WebUtility.HtmlDecode(await deleteResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"pendingQueue\"", deleteHtml);
        Assert.Contains($"/PrintJobs/Details/{printJobId}?returnTo=pendingQueue", deleteHtml);

        var token = ExtractAntiForgeryToken(deleteHtml);
        var postResponse = await client.PostAsync(
            $"/PrintJobs/Delete/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = printJobId.ToString(),
                ["returnTo"] = "pendingQueue"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/PrintImports/Details/{pendingImportId}?returnTo=pendingQueue", postResponse.Headers.Location?.ToString());

        var importDetailsResponse = await client.GetAsync($"/PrintImports/Details/{pendingImportId}?returnTo=pendingQueue");
        var importDetailsHtml = WebUtility.HtmlDecode(await importDetailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, importDetailsResponse.StatusCode);
        Assert.Contains("Voltar para pendentes", importDetailsHtml);
        Assert.Contains($"/PrintImports/CreatePrintJob/{pendingImportId}?returnTo=pendingQueue", importDetailsHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedPrintJob = await context.PrintJobs.SingleOrDefaultAsync(printJob => printJob.Id == printJobId);
        var import = await context.PrintImports.SingleAsync(printImport => printImport.Id == pendingImportId);

        Assert.Null(deletedPrintJob);
        Assert.Equal(PrintImportStatus.Parsed, import.Status);
    }

    [Fact]
    public async Task PrintImportsIndex_ShowsProductionState_ForPendingAndLinkedParsedImports()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var pendingImportId = Guid.NewGuid();
        var linkedImportId = Guid.NewGuid();
        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = pendingImportId,
                    FileName = "pending-generation.gcode",
                    FileType = "gcode",
                    ParsedDataJson = """
                    {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                    """,
                    Status = PrintImportStatus.Parsed,
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = linkedImportId,
                    FileName = "linked-generation.gcode",
                    FileType = "gcode",
                    ParsedDataJson = """
                    {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                    """,
                    Status = PrintImportStatus.Parsed,
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                });

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                PrintImportId = linkedImportId,
                SourceFileName = "linked-generation.gcode",
                FilamentUsedGrams = 12.45m,
                Status = PrintJobStatus.Imported,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var indexResponse = await client.GetAsync("/PrintImports");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Produção", indexHtml);
        Assert.Contains("pending-generation.gcode", indexHtml);
        Assert.Contains("Pendente", indexHtml);
        Assert.Contains($"/PrintImports/CreatePrintJob/{pendingImportId}", indexHtml);
        Assert.Contains("Gerar produção", indexHtml);
        Assert.Contains("linked-generation.gcode", indexHtml);
        Assert.Contains("Vinculada", indexHtml);
        Assert.Contains($"/PrintJobs/Details/{printJobId}", indexHtml);
        Assert.Contains("Ver produção", indexHtml);

        var pendingResponse = await client.GetAsync("/PrintImports?productionState=pending");
        var pendingHtml = WebUtility.HtmlDecode(await pendingResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, pendingResponse.StatusCode);
        Assert.Contains("Pendentes de produção (", pendingHtml);
        Assert.Contains("pending-generation.gcode", pendingHtml);
        Assert.Contains($"/PrintImports/CreatePrintJob/{pendingImportId}", pendingHtml);
        Assert.DoesNotContain("linked-generation.gcode", pendingHtml);
        Assert.DoesNotContain($"/PrintJobs/Details/{printJobId}", pendingHtml);
    }

    [Fact]
    public async Task Dashboard_LinksPendingProductionImports_ToFilteredImportReview()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var pendingImportId = Guid.NewGuid();
        var linkedImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = pendingImportId,
                    FileName = "dashboard-pending.gcode",
                    FileType = "gcode",
                    ParsedDataJson = """
                    {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                    """,
                    Status = PrintImportStatus.Parsed,
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = linkedImportId,
                    FileName = "dashboard-linked.gcode",
                    FileType = "gcode",
                    ParsedDataJson = """
                    {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                    """,
                    Status = PrintImportStatus.Parsed,
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                });

            context.PrintJobs.Add(new PrintJob
            {
                Id = Guid.NewGuid(),
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                PrintImportId = linkedImportId,
                SourceFileName = "dashboard-linked.gcode",
                FilamentUsedGrams = 12.45m,
                Status = PrintJobStatus.Imported,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Pendentes de produção", dashboardHtml);
        Assert.Contains(">1<", dashboardHtml);
        Assert.Contains("/PrintImports?productionState=pending", dashboardHtml);
        Assert.Contains("Revisar importações", dashboardHtml);
    }

    [Fact]
    public async Task Dashboard_LinksFailedImports_ToRecoveryQueue()
    {
        using var factory = new ThreeDManagerWebFactory();
        var retryableImportId = Guid.NewGuid();
        var blockedImportId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintImports.AddRange(
                new PrintImport
                {
                    Id = retryableImportId,
                    FileName = "dashboard-retryable-failed.gcode",
                    FileType = "gcode",
                    RawContent = ";FLAVOR:Marlin\n;Generated by Creality Print\n;Filament used: 1.23m, 12.45g\n;TIME:3600\n",
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Falha transitória de parser",
                    ImportedAt = DateTime.UtcNow
                },
                new PrintImport
                {
                    Id = blockedImportId,
                    FileName = "dashboard-reimport-failed.gcode",
                    FileType = "gcode",
                    RawContent = string.Empty,
                    Status = PrintImportStatus.Error,
                    ErrorMessage = "Conteúdo ausente",
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                },
                new PrintImport
                {
                    Id = Guid.NewGuid(),
                    FileName = "dashboard-uploaded.gcode",
                    FileType = "gcode",
                    RawContent = "; uploaded import",
                    Status = PrintImportStatus.Uploaded,
                    ImportedAt = DateTime.UtcNow.AddMinutes(-1)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Importações com falha", dashboardHtml);
        Assert.Contains(">2<", dashboardHtml);
        Assert.Contains("dashboard-retryable-failed.gcode", dashboardHtml);
        Assert.Contains("dashboard-reimport-failed.gcode", dashboardHtml);
        Assert.Contains("Pode tentar novamente", dashboardHtml);
        Assert.Contains("Reimportar arquivo", dashboardHtml);
        Assert.Contains("/PrintImports?status=error", dashboardHtml);
        Assert.Contains("Revisar falhas", dashboardHtml);
        Assert.Contains($"/PrintImports/Process/{retryableImportId}", dashboardHtml);
        Assert.DoesNotContain($"/PrintImports/Process/{blockedImportId}", dashboardHtml);
        Assert.Contains("Tentar novamente", dashboardHtml);
        Assert.Contains("name=\"returnTo\" value=\"dashboard\"", dashboardHtml);

        var token = ExtractAntiForgeryToken(dashboardHtml);
        var retryResponse = await client.PostAsync(
            $"/PrintImports/Process/{retryableImportId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["returnTo"] = "dashboard"
            }));

        Assert.Equal(HttpStatusCode.Redirect, retryResponse.StatusCode);
        Assert.Equal("/Dashboard", retryResponse.Headers.Location?.ToString());

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var retryableImport = await context.PrintImports.SingleAsync(printImport => printImport.Id == retryableImportId);

        Assert.Equal(PrintImportStatus.Parsed, retryableImport.Status);
        Assert.False(string.IsNullOrWhiteSpace(retryableImport.ParsedDataJson));

        var refreshedDashboardResponse = await client.GetAsync("/Dashboard");
        var refreshedDashboardHtml = WebUtility.HtmlDecode(await refreshedDashboardResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, refreshedDashboardResponse.StatusCode);
        Assert.Contains("Arquivo processado com sucesso.", refreshedDashboardHtml);
    }

    [Fact]
    public async Task MaterialsDetails_ShowsStockMovementHistory_AfterManualAdjustment()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Materials/Edit/{ids.MaterialId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            $"/Materials/Edit/{ids.MaterialId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = ids.MaterialId.ToString(),
                ["Name"] = "PLA Preto",
                ["Type"] = "PLA",
                ["Brand"] = "E2E",
                ["Color"] = "Black",
                ["CostPerKg"] = "80.00",
                ["CurrentStockGrams"] = "900.00",
                ["CreatedAt"] = "2026-06-19T02:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var detailsResponse = await client.GetAsync($"/Materials/Details/{ids.MaterialId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Ajuste manual", detailsHtml);
        Assert.Contains("900,00 g", detailsHtml);
        Assert.Contains("Custo por grama", detailsHtml);
        Assert.Contains((80m / 1000m).ToString("C4"), detailsHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var movement = await context.MaterialStockMovements.SingleAsync(movement => movement.MaterialId == ids.MaterialId);

        Assert.Equal("ManualAdjustment", movement.MovementType);
        Assert.Equal(-100m, movement.QuantityGrams);
        Assert.Equal(1000m, movement.StockBeforeGrams);
        Assert.Equal(900m, movement.StockAfterGrams);
    }

    [Fact]
    public async Task AdjustStock_AddRemoveAndSet_UpdatesMaterialStockAndRecordsMovement()
    {
        var scenarios = new[]
        {
            new { InitialStock = 1000m, AdjustmentType = "Add", Quantity = 500m, ExpectedStock = 1500m, ExpectedMovement = 500m },
            new { InitialStock = 1000m, AdjustmentType = "Remove", Quantity = 100m, ExpectedStock = 900m, ExpectedMovement = -100m },
            new { InitialStock = 1200m, AdjustmentType = "Set", Quantity = 1000m, ExpectedStock = 1000m, ExpectedMovement = -200m }
        };

        foreach (var scenario in scenarios)
        {
            using var factory = new ThreeDManagerWebFactory();
            var ids = await SeedCommonAsync(factory, stockGrams: scenario.InitialStock);
            using var client = factory.CreateTestClient();

            var getResponse = await client.GetAsync($"/Materials/AdjustStock/{ids.MaterialId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
            var postResponse = await client.PostAsync(
                "/Materials/AdjustStock",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["MaterialId"] = ids.MaterialId.ToString(),
                    ["MaterialName"] = "PLA Preto",
                    ["CurrentStockGrams"] = scenario.InitialStock.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    ["AdjustmentType"] = scenario.AdjustmentType,
                    ["QuantityGrams"] = scenario.Quantity.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    ["Notes"] = $"Operação {scenario.AdjustmentType}"
                }));

            Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
            Assert.Equal($"/Materials/Details/{ids.MaterialId}", postResponse.Headers.Location?.ToString());

            using var verifyScope = factory.Services.CreateScope();
            var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
            var movement = await context.MaterialStockMovements.SingleAsync(movement => movement.MaterialId == ids.MaterialId);

            Assert.Equal(scenario.ExpectedStock, material.CurrentStockGrams);
            Assert.Equal("ManualAdjustment", movement.MovementType);
            Assert.Equal(scenario.ExpectedMovement, movement.QuantityGrams);
            Assert.Equal(scenario.InitialStock, movement.StockBeforeGrams);
            Assert.Equal(scenario.ExpectedStock, movement.StockAfterGrams);
        }
    }

    [Fact]
    public async Task AdjustStock_RemoveRejects_WhenItWouldMakeStockNegative()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory, stockGrams: 25m);
        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Materials/AdjustStock/{ids.MaterialId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            "/Materials/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["MaterialName"] = "PLA Preto",
                ["CurrentStockGrams"] = "25.00",
                ["AdjustmentType"] = "Remove",
                ["QuantityGrams"] = "30.00",
                ["Notes"] = "Teste de proteção"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("A remoção não pode deixar o estoque negativo.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
        var movements = await context.MaterialStockMovements.Where(movement => movement.MaterialId == ids.MaterialId).ToListAsync();

        Assert.Equal(25m, material.CurrentStockGrams);
        Assert.Empty(movements);
    }

    [Fact]
    public async Task MaterialEdit_RejectsNegativeStock_WithoutPersistingChangeOrMovement()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory, stockGrams: 1000m);
        using var client = factory.CreateTestClient();

        var editPage = await client.GetAsync($"/Materials/Edit/{ids.MaterialId}");
        Assert.Equal(HttpStatusCode.OK, editPage.StatusCode);

        var token = ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            $"/Materials/Edit/{ids.MaterialId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = ids.MaterialId.ToString(),
                ["Name"] = "PLA Preto",
                ["Type"] = "PLA",
                ["Brand"] = "E2E",
                ["Color"] = "Black",
                ["CostPerKg"] = "80.00",
                ["CurrentStockGrams"] = "-50.00",
                ["MinimumStockGrams"] = "200.00",
                ["CreatedAt"] = "2026-06-19T02:00:00Z"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("O estoque atual não pode ser negativo.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
        var movements = await context.MaterialStockMovements.Where(movement => movement.MaterialId == ids.MaterialId).ToListAsync();

        Assert.Equal(1000m, material.CurrentStockGrams);
        Assert.Empty(movements);
    }

    [Fact]
    public async Task ProductEdit_RejectsNegativeSalePrice_WithoutPersistingChange()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        using var client = factory.CreateTestClient();

        var editPage = await client.GetAsync($"/Products/Edit/{ids.ProductId}");
        Assert.Equal(HttpStatusCode.OK, editPage.StatusCode);

        var token = ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            $"/Products/Edit/{ids.ProductId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = ids.ProductId.ToString(),
                ["Name"] = "Product A",
                ["Sku"] = "E2E-PROD",
                ["Category"] = "Brindes",
                ["Description"] = "Produto de teste",
                ["SalePrice"] = "-10.00",
                ["IsActive"] = "true",
                ["CreatedAt"] = "2026-06-30T02:00:00Z"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("O preço de venda não pode ser negativo.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);

        Assert.Null(product.SalePrice);
    }

    [Fact]
    public async Task PrinterEdit_RejectsMissingName_WithoutPersistingChange()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        using var client = factory.CreateTestClient();

        var editPage = await client.GetAsync($"/Printers/Edit/{ids.PrinterId}");
        Assert.Equal(HttpStatusCode.OK, editPage.StatusCode);

        var token = ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            $"/Printers/Edit/{ids.PrinterId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = ids.PrinterId.ToString(),
                ["Name"] = "",
                ["Brand"] = "Alterada",
                ["Model"] = "Model B",
                ["Notes"] = "Sem nome",
                ["CreatedAt"] = "2026-06-30T02:00:00Z"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Informe o nome da impressora.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printer = await context.Printers.SingleAsync(printer => printer.Id == ids.PrinterId);

        Assert.Equal("Printer A", printer.Name);
        Assert.Equal("E2E", printer.Brand);
    }

    [Fact]
    public async Task MaterialsIndex_ShowsLatestStockMovementSummary_AfterManualAdjustment()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Materials/AdjustStock/{ids.MaterialId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            "/Materials/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["MaterialName"] = "PLA Preto",
                ["CurrentStockGrams"] = "1000.00",
                ["AdjustmentType"] = "Add",
                ["QuantityGrams"] = "500.00",
                ["Notes"] = "Compra de rolo novo"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var indexResponse = await client.GetAsync("/Materials");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Movimentação recente", indexHtml);
        Assert.Contains("Compra de rolo novo", indexHtml);
        Assert.Contains("+500,00 g", indexHtml);
    }

    [Fact]
    public async Task MaterialsIndex_ShowsDerivedCostPerGram_FromCostPerKg()
    {
        using var factory = new ThreeDManagerWebFactory();
        await SeedCommonAsync(factory);
        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/Materials");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Custo por grama", indexHtml);
        // Seeded material has CostPerKg = 80, so cost per gram = 80 / 1000 = R$ 0,0800.
        Assert.Contains("R$ 0,0800", indexHtml);
    }

    [Fact]
    public async Task LowStockAlert_IsShown_OnMaterialsAndDashboard_WhenBelowMinimum()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        using var client = factory.CreateTestClient();

        var editPage = await client.GetAsync($"/Materials/Edit/{ids.MaterialId}");
        Assert.Equal(HttpStatusCode.OK, editPage.StatusCode);

        var token = ExtractAntiForgeryToken(await editPage.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            $"/Materials/Edit/{ids.MaterialId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = ids.MaterialId.ToString(),
                ["Name"] = "PLA Preto",
                ["Type"] = "PLA",
                ["Brand"] = "E2E",
                ["Color"] = "Black",
                ["CostPerKg"] = "80.00",
                ["CurrentStockGrams"] = "120.00",
                ["MinimumStockGrams"] = "200.00",
                ["CreatedAt"] = "2026-06-19T02:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var detailsResponse = await client.GetAsync($"/Materials/Details/{ids.MaterialId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Estoque mínimo", detailsHtml);
        Assert.Contains("Baixo estoque", detailsHtml);

        var indexResponse = await client.GetAsync("/Materials");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Baixo estoque", indexHtml);
        Assert.Contains("120,00 g", indexHtml);
        Assert.Contains("200,00 g", indexHtml);

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Alertas de estoque", dashboardHtml);
        Assert.Contains("Existem <strong>1</strong> materiais em baixo estoque.", dashboardHtml);
        Assert.Contains("PLA Preto", dashboardHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);

        Assert.Equal(120m, material.CurrentStockGrams);
        Assert.Equal(200m, material.MinimumStockGrams);
    }

    [Fact]
    public async Task EditPrintJob_ToCompleted_DeductsStockThroughMvcPipeline()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "legacy.gcode",
                FilamentUsedGrams = 12.45m,
                FilamentUsedMeters = 1.23m,
                ReportedCost = 2.50m,
                Status = "Imported",
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            $"/PrintJobs/Edit/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = printJobId.ToString(),
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["PrintImportId"] = "",
                ["SourceFileName"] = "legacy.gcode",
                ["CreatedAt"] = "2026-06-19T02:00:00Z",
                ["FilamentUsedGrams"] = "20.00",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Completed"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/PrintJobs/Details/{printJobId}", postResponse.Headers.Location?.ToString());

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.Id == printJobId);

        Assert.Equal(980m, material.CurrentStockGrams);
        Assert.Equal("Completed", printJob.Status);
        Assert.Equal(20m, printJob.StockDeductedGrams);
        Assert.Equal(material.Id, printJob.StockDeductedMaterialId);
    }

    [Fact]
    public async Task EditPrintJob_RejectsUnknownStatus_WithoutChangingStockOrJob()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "legacy.gcode",
                FilamentUsedGrams = 12.45m,
                FilamentUsedMeters = 1.23m,
                ReportedCost = 2.50m,
                Status = "Imported",
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            $"/PrintJobs/Edit/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = printJobId.ToString(),
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["PrintImportId"] = "",
                ["SourceFileName"] = "legacy.gcode",
                ["CreatedAt"] = "2026-06-19T02:00:00Z",
                ["FilamentUsedGrams"] = "20.00",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Done"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Selecione um status de produção válido.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.Id == printJobId);

        Assert.Equal(1000m, material.CurrentStockGrams);
        Assert.Equal("Imported", printJob.Status);
        Assert.Equal(12.45m, printJob.FilamentUsedGrams);
        Assert.Null(printJob.StockDeductedGrams);
    }

    [Fact]
    public async Task EditPrintJob_RejectsNegativeFilamentGrams_WithoutPersistingChange()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "legacy.gcode",
                FilamentUsedGrams = 12.45m,
                FilamentUsedMeters = 1.23m,
                ReportedCost = 2.50m,
                Status = "Imported",
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            $"/PrintJobs/Edit/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["Id"] = printJobId.ToString(),
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["PrintImportId"] = "",
                ["SourceFileName"] = "legacy.gcode",
                ["CreatedAt"] = "2026-06-19T02:00:00Z",
                ["FilamentUsedGrams"] = "-5.00",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Imported"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("O filamento usado (g) não pode ser negativo.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.Id == printJobId);

        Assert.Equal(12.45m, printJob.FilamentUsedGrams);
        Assert.Equal(2.50m, printJob.ReportedCost);
    }

    [Fact]
    public async Task CreatePrintJob_FromImport_RejectsNegativeFilamentGrams_WithoutCreatingPrintJob()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "sample.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            "/PrintImports/CreatePrintJob",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ImportId"] = importId.ToString(),
                ["FileName"] = "sample.gcode",
                ["ParsedMaterialType"] = "PLA",
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["FilamentUsedGrams"] = "-5.00",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Imported"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("O filamento usado (g) não pode ser negativo.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasPrintJobForImport = await context.PrintJobs.AnyAsync(printJob => printJob.PrintImportId == importId);
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);

        Assert.False(hasPrintJobForImport);
        Assert.Equal(1000m, material.CurrentStockGrams);
    }

    [Fact]
    public async Task CreatePrintJob_FromImport_ShowsDerivedMaterialCostPerGram_FromMaterialCostPerKg()
    {
        using var factory = new ThreeDManagerWebFactory();
        await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "sample.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var getResponse = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}");
        var html = WebUtility.HtmlDecode(await getResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("Custo por grama do material", html);
        Assert.Contains("Custo estimado do material", html);
        // Seeded material CostPerKg = 80 -> 80 / 1000 = 0.08 (invariant) in the option data attribute.
        Assert.Contains("data-cost-per-gram=\"0.08\"", html);
        Assert.Contains("filamentUsedGrams.addEventListener(\"input\", updateCostHints)", html);
    }

    [Fact]
    public async Task PrintJobsViews_ShowLocalizedStatusLabels_InIndexDetailsDeleteAndEdit()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var completedPrintJobId = Guid.NewGuid();
        var canceledPrintJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = completedPrintJobId,
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "completed.gcode",
                    Status = PrintJobStatus.Completed,
                    CreatedAt = DateTime.UtcNow
                },
                new PrintJob
                {
                    Id = canceledPrintJobId,
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "canceled.gcode",
                    Status = PrintJobStatus.Canceled,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-5)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/PrintJobs");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("<th>Custo calculado do material</th>", indexHtml);
        Assert.Contains("<th>Status da produção</th>", indexHtml);
        Assert.Contains("Concluída", indexHtml);
        Assert.Contains("Cancelada", indexHtml);
        Assert.DoesNotContain("<th>Custo mat.</th>", indexHtml);
        Assert.DoesNotContain("<th>Status</th>", indexHtml);
        Assert.DoesNotContain(">Completed<", indexHtml);
        Assert.DoesNotContain(">Canceled<", indexHtml);

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{completedPrintJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Status da produção", detailsHtml);
        Assert.Contains("Concluída", detailsHtml);
        Assert.DoesNotContain(">Status</dt>", detailsHtml);
        Assert.DoesNotContain(">Completed<", detailsHtml);

        var deleteResponse = await client.GetAsync($"/PrintJobs/Delete/{canceledPrintJobId}");
        var deleteHtml = WebUtility.HtmlDecode(await deleteResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Contains("Status da produção", deleteHtml);
        Assert.Contains("Cancelada", deleteHtml);
        Assert.DoesNotContain(">Status</dt>", deleteHtml);
        Assert.DoesNotContain(">Canceled<", deleteHtml);

        var editResponse = await client.GetAsync($"/PrintJobs/Edit/{completedPrintJobId}");
        var editHtml = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        Assert.Contains("Status da produção", editHtml);
        Assert.Contains(">Concluída</option>", editHtml);
        Assert.Contains(">Cancelada</option>", editHtml);
        Assert.DoesNotContain(">Status</label>", editHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_ShowsDerivedMaterialCostPerGram_FromMaterialCostPerKg()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "cost-per-gram.gcode",
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Custo por grama do material", detailsHtml);
        // Seeded CostPerKg = 80 -> 80 / 1000 = 0,0800 formatted as C4.
        Assert.Contains("R$ 0,0800", detailsHtml);
    }

    [Fact]
    public async Task Dashboard_ShowsLocalizedStatusLabels_InRecentPrintJobsTable()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var completedPrintJobId = Guid.NewGuid();
        var canceledPrintJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = completedPrintJobId,
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-completed.gcode",
                    Status = PrintJobStatus.Completed,
                    CreatedAt = DateTime.UtcNow.AddMinutes(10)
                },
                new PrintJob
                {
                    Id = canceledPrintJobId,
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-canceled.gcode",
                    Status = PrintJobStatus.Canceled,
                    CreatedAt = DateTime.UtcNow.AddMinutes(9)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());
        var recentPrintJobsSection = dashboardHtml[dashboardHtml.IndexOf("Últimas produções", StringComparison.Ordinal)..];

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("dashboard-completed.gcode", dashboardHtml);
        Assert.Contains("dashboard-canceled.gcode", dashboardHtml);
        Assert.Contains("badge bg-success", dashboardHtml);
        Assert.Contains("badge bg-dark", dashboardHtml);
        Assert.Contains("<th>Filamento usado</th>", recentPrintJobsSection);
        Assert.Contains("<th>Tempo estimado</th>", recentPrintJobsSection);
        Assert.Contains("<th>Custos da produção</th>", recentPrintJobsSection);
        Assert.Contains("<th>Status da produção</th>", recentPrintJobsSection);
        Assert.DoesNotContain("<th>Filamento</th>", recentPrintJobsSection);
        Assert.Contains("Custo informado:", recentPrintJobsSection);
        Assert.Contains("Custo calculado do material: não disponível", recentPrintJobsSection);
        Assert.DoesNotContain("<th>Tempo</th>", recentPrintJobsSection);
        Assert.DoesNotContain("<th>Custo</th>", recentPrintJobsSection);
        Assert.DoesNotContain("Arquivo:", recentPrintJobsSection);
        Assert.DoesNotContain("Material:", recentPrintJobsSection);
        Assert.Contains("Concluída", dashboardHtml);
        Assert.Contains("Cancelada", dashboardHtml);
        Assert.DoesNotContain("<th>Status</th>", recentPrintJobsSection);
        Assert.DoesNotContain(">Completed<", dashboardHtml);
        Assert.DoesNotContain(">Canceled<", dashboardHtml);
    }

    [Fact]
    public async Task CreatePrintJob_View_UsesLocalizedStatusOptions()
    {
        using var factory = new ThreeDManagerWebFactory();
        await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "status-options.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":60,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
                """,
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var response = await client.GetAsync($"/PrintImports/CreatePrintJob/{importId}");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Status da produção", html);
        Assert.Contains(">Importada</option>", html);
        Assert.Contains(">Planejada</option>", html);
        Assert.Contains(">Concluída</option>", html);
        Assert.Contains(">Falhou</option>", html);
        Assert.Contains(">Cancelada</option>", html);
        Assert.DoesNotContain(">Imported<", html);
        Assert.DoesNotContain(">Planned<", html);
        Assert.DoesNotContain(">Completed<", html);
        Assert.DoesNotContain(">Failed<", html);
        Assert.DoesNotContain(">Canceled<", html);
    }

    private static async Task<(Guid MaterialId, Guid ProductId, Guid PrinterId)> SeedCommonAsync(
        ThreeDManagerWebFactory factory,
        decimal stockGrams = 1000m)
    {
        var materialId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var printerId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Materials.Add(new Material
            {
                Id = materialId,
                Name = "PLA Preto",
                Type = "PLA",
                Brand = "E2E",
                Color = "Black",
                CostPerKg = 80m,
                CurrentStockGrams = stockGrams,
                CreatedAt = DateTime.UtcNow
            });

            context.Products.Add(new Product
            {
                Id = productId,
                Name = "Product A",
                Sku = "E2E-PROD",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            context.Printers.Add(new Printer
            {
                Id = printerId,
                Name = "Printer A",
                Brand = "E2E",
                Model = "Model A",
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        return (materialId, productId, printerId);
    }

    private static async Task<Guid> GetCreatedPrintJobIdAsync(ThreeDManagerWebFactory factory, Guid importId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.PrintJobs
            .Where(printJob => printJob.PrintImportId == importId)
            .Select(printJob => printJob.Id)
            .SingleAsync();
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Could not find antiforgery token in form html.");
        return match.Groups[1].Value;
    }
}
