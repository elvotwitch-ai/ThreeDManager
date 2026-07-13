using System.Globalization;
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
    public async Task AlphaAccess_RequiresLogin_RejectsInvalidPassword_AndSupportsLogout()
    {
        using var factory = new ThreeDManagerWebFactory(bypassAuthentication: false);
        using var client = factory.CreateTestClient();

        var protectedResponse = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, protectedResponse.StatusCode);
        Assert.Equal("/Account/Login", protectedResponse.Headers.Location?.AbsolutePath);

        var loginResponse = await client.GetAsync("/Account/Login?returnUrl=%2FDashboard");
        var loginHtml = await loginResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Contains("Acesso ao ThreeDManager", loginHtml);

        var invalidResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ExtractAntiForgeryToken(loginHtml),
                ["Username"] = "alpha-operator",
                ["Password"] = "wrong-password",
                ["ReturnUrl"] = "/Dashboard"
            }));
        var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        Assert.Contains("Usuário ou senha inválidos.", WebUtility.HtmlDecode(invalidHtml));

        var validResponse = await client.PostAsync(
            "/Account/Login",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ExtractAntiForgeryToken(invalidHtml),
                ["Username"] = "alpha-operator",
                ["Password"] = "test-only-password",
                ["ReturnUrl"] = "/Dashboard"
            }));
        Assert.Equal(HttpStatusCode.Redirect, validResponse.StatusCode);
        Assert.Equal("/Dashboard", validResponse.Headers.Location?.ToString());

        var authenticatedResponse = await client.GetAsync("/Dashboard");
        var authenticatedHtml = await authenticatedResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, authenticatedResponse.StatusCode);
        Assert.Contains("alpha-operator", authenticatedHtml);
        Assert.Contains(">Sair</button>", authenticatedHtml);

        var logoutResponse = await client.PostAsync(
            "/Account/Logout",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = ExtractAntiForgeryToken(authenticatedHtml)
            }));
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Equal("/Account/Login", logoutResponse.Headers.Location?.ToString());

        var afterLogoutResponse = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, afterLogoutResponse.StatusCode);
    }

    [Fact]
    public async Task SharedLayout_DefaultsToDarkTheme_AndRendersPersistentToggle()
    {
        using var factory = new ThreeDManagerWebFactory();
        using var client = factory.CreateTestClient();

        var response = await client.GetAsync("/Dashboard");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<html lang=\"pt-BR\" data-bs-theme=\"dark\">", html);
        Assert.Contains("id=\"theme-toggle\"", html);
        Assert.Contains("threedmanager-theme", html);
        Assert.Contains("Modo claro", html);
    }

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

        var uploadedResponse = await client.GetAsync("/PrintImports?status=uploaded");
        var uploadedHtml = WebUtility.HtmlDecode(await uploadedResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, uploadedResponse.StatusCode);
        Assert.Contains("Aguardando processamento (", uploadedHtml);
        Assert.Contains("uploaded-filter.gcode", uploadedHtml);
        Assert.DoesNotContain("retryable-filter-error.gcode", uploadedHtml);
        Assert.DoesNotContain("blocked-filter-error.gcode", uploadedHtml);
        Assert.DoesNotContain("pending-filter.gcode", uploadedHtml);
        Assert.Contains($"/PrintImports/Details/{uploadedImportId}?returnTo=uploadedQueue", uploadedHtml);
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
    public async Task MaterialsDetails_ShowsInventoryValue_FromCostPerKgAndCurrentStock()
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
        Assert.Contains("Valor em estoque", detailsHtml);
        // R$ 80,00/kg -> R$ 0,08/g; 900 g on hand -> R$ 72,00.
        Assert.Contains(72.00m.ToString("C"), detailsHtml);
    }

    [Fact]
    public async Task ProductsDetails_ShowsStockValue_FromSalePriceAndStockQuantity()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Products/Edit/{ids.ProductId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
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
                ["SalePrice"] = "25.00",
                ["StockQuantity"] = "8",
                ["IsActive"] = "true",
                ["CreatedAt"] = "2026-06-30T02:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var detailsResponse = await client.GetAsync($"/Products/Details/{ids.ProductId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Valor em estoque", detailsHtml);
        // R$ 25,00 each; 8 units on hand -> R$ 200,00.
        Assert.Contains(200.00m.ToString("C"), detailsHtml);
    }

    [Fact]
    public async Task ProductsIndex_ShowsTotalStockValue_SummedAcrossProducts()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        // Set the seeded product to a known SalePrice x StockQuantity -> R$ 200,00.
        using (var client = factory.CreateTestClient())
        {
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
                    ["SalePrice"] = "25.00",
                    ["StockQuantity"] = "8",
                    ["IsActive"] = "true",
                    ["CreatedAt"] = "2026-06-30T02:00:00Z"
                }));
            Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        }

        // Add a second product -> R$ 120,00.
        await factory.SeedAsync(async context =>
        {
            context.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = "Chaveiro Azul",
                Sku = "E2E-KEY",
                Category = "Brindes",
                SalePrice = 40m,
                StockQuantity = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        });

        using var readClient = factory.CreateTestClient();
        var indexResponse = await readClient.GetAsync("/Products");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Valor total em estoque", indexHtml);
        // R$ 200,00 (Product A) + R$ 120,00 (Chaveiro Azul) = R$ 320,00.
        Assert.Contains("R$ 320,00", indexHtml);
    }

    [Fact]
    public async Task Dashboard_ShowsInventoryValueTile_SummingMaterialsAndFinishedGoods()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory); // Material PLA Preto: 80/kg x 1000 g = R$ 80,00; Product A: no price -> R$ 0,00.

        // Set the seeded product to a known SalePrice x StockQuantity -> R$ 200,00.
        using (var client = factory.CreateTestClient())
        {
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
                    ["SalePrice"] = "25.00",
                    ["StockQuantity"] = "8",
                    ["IsActive"] = "true",
                    ["CreatedAt"] = "2026-06-30T02:00:00Z"
                }));
            Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        }

        await factory.SeedAsync(async context =>
        {
            // Second material -> R$ 50,00, so materials total R$ 130,00.
            context.Materials.Add(new Material
            {
                Id = Guid.NewGuid(),
                Name = "PETG Azul",
                Type = "PETG",
                Brand = "E2E",
                Color = "Blue",
                CostPerKg = 100m,
                CurrentStockGrams = 500m,
                CreatedAt = DateTime.UtcNow
            });

            // Second product -> R$ 120,00, so finished goods total R$ 320,00.
            context.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = "Chaveiro Azul",
                Sku = "E2E-KEY",
                Category = "Brindes",
                SalePrice = 40m,
                StockQuantity = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var readClient = factory.CreateTestClient();
        var dashboardResponse = await readClient.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Valor em estoque", dashboardHtml);
        // Materials: R$ 80,00 (PLA) + R$ 50,00 (PETG) = R$ 130,00.
        Assert.Contains("Materiais: R$ 130,00", dashboardHtml);
        // Finished goods: R$ 200,00 (Product A) + R$ 120,00 (Chaveiro Azul) = R$ 320,00.
        Assert.Contains("Produtos acabados: R$ 320,00", dashboardHtml);
        // Combined inventory value -> R$ 450,00.
        Assert.Contains("R$ 450,00", dashboardHtml);
        // The Materiais / Produtos acabados figures drill down to their index lists.
        Assert.Matches(
            "<a[^>]*data-inventory-value-link=\"materials\"[^>]*href=\"/Materials\"|<a[^>]*href=\"/Materials\"[^>]*data-inventory-value-link=\"materials\"",
            dashboardHtml);
        Assert.Matches(
            "<a[^>]*data-inventory-value-link=\"products\"[^>]*href=\"/Products\"|<a[^>]*href=\"/Products\"[^>]*data-inventory-value-link=\"products\"",
            dashboardHtml);
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
    public async Task ProductAdjustStock_AddRemoveAndSet_UpdatesProductStockAndRecordsMovement()
    {
        var scenarios = new[]
        {
            new { InitialStock = 10, AdjustmentType = "Add", Quantity = 5, ExpectedStock = 15, ExpectedMovement = 5 },
            new { InitialStock = 10, AdjustmentType = "Remove", Quantity = 3, ExpectedStock = 7, ExpectedMovement = -3 },
            new { InitialStock = 12, AdjustmentType = "Set", Quantity = 20, ExpectedStock = 20, ExpectedMovement = 8 }
        };

        foreach (var scenario in scenarios)
        {
            using var factory = new ThreeDManagerWebFactory();
            var ids = await SeedCommonAsync(factory);
            await factory.SeedAsync(async context =>
            {
                var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
                product.StockQuantity = scenario.InitialStock;
                await context.SaveChangesAsync();
            });
            using var client = factory.CreateTestClient();

            var getResponse = await client.GetAsync($"/Products/AdjustStock/{ids.ProductId}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
            var postResponse = await client.PostAsync(
                "/Products/AdjustStock",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = token,
                    ["ProductId"] = ids.ProductId.ToString(),
                    ["ProductName"] = "Product A",
                    ["CurrentStockQuantity"] = scenario.InitialStock.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["AdjustmentType"] = scenario.AdjustmentType,
                    ["QuantityUnits"] = scenario.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["Notes"] = $"Operação {scenario.AdjustmentType}"
                }));

            Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
            Assert.Equal($"/Products/Details/{ids.ProductId}", postResponse.Headers.Location?.ToString());

            var detailsResponse = await client.GetAsync($"/Products/Details/{ids.ProductId}");
            var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
            Assert.Contains("Movimentações de estoque", detailsHtml);
            Assert.Contains("Ajuste manual", detailsHtml);
            Assert.Contains(scenario.ExpectedMovement < 0 ? "text-danger" : "text-success", detailsHtml);
            Assert.Contains($"{scenario.ExpectedStock} un.", detailsHtml);
            Assert.Contains($"Operação {scenario.AdjustmentType}", detailsHtml);

            var indexResponse = await client.GetAsync("/Products");
            var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
            Assert.Contains("Movimentação recente", indexHtml);
            Assert.Contains("Ajuste manual", indexHtml);
            Assert.Contains($"{(scenario.ExpectedMovement > 0 ? "+" : string.Empty)}{scenario.ExpectedMovement} un.", indexHtml);
            Assert.Contains($"Operação {scenario.AdjustmentType}", indexHtml);

            var dashboardResponse = await client.GetAsync("/Dashboard");
            var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
            Assert.Contains("data-dashboard-inventory-movement-grid", dashboardHtml);
            Assert.Contains("Movimentações de estoque recentes", dashboardHtml);
            Assert.Contains("Movimentações de estoque de produtos recentes", dashboardHtml);
            Assert.Contains("Product A", dashboardHtml);
            Assert.Contains("Ajuste manual", dashboardHtml);
            Assert.Contains($"{(scenario.ExpectedMovement > 0 ? "+" : string.Empty)}{scenario.ExpectedMovement} un.", dashboardHtml);
            Assert.Contains($"{scenario.ExpectedStock} un.", dashboardHtml);
            Assert.Contains($"Operação {scenario.AdjustmentType}", dashboardHtml);

            using var verifyScope = factory.Services.CreateScope();
            var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
            var movement = await context.ProductStockMovements.SingleAsync(movement => movement.ProductId == ids.ProductId);

            Assert.Equal(scenario.ExpectedStock, product.StockQuantity);
            Assert.Equal("ManualAdjustment", movement.MovementType);
            Assert.Equal(scenario.ExpectedMovement, movement.QuantityUnits);
            Assert.Equal(scenario.InitialStock, movement.StockBeforeUnits);
            Assert.Equal(scenario.ExpectedStock, movement.StockAfterUnits);
        }
    }

    [Fact]
    public async Task ProductAdjustStock_RemoveRejects_WhenItWouldMakeStockNegative()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        await factory.SeedAsync(async context =>
        {
            var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
            product.StockQuantity = 2;
            await context.SaveChangesAsync();
        });
        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Products/AdjustStock/{ids.ProductId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var postResponse = await client.PostAsync(
            "/Products/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ProductId"] = ids.ProductId.ToString(),
                ["ProductName"] = "Product A",
                ["CurrentStockQuantity"] = "2",
                ["AdjustmentType"] = "Remove",
                ["QuantityUnits"] = "5",
                ["Notes"] = "Teste de proteção"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("A remoção não pode deixar o estoque negativo.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
        var movements = await context.ProductStockMovements.Where(movement => movement.ProductId == ids.ProductId).ToListAsync();

        Assert.Equal(2, product.StockQuantity);
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
    public async Task ProductEdit_PersistsAndRendersStockQuantity()
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
                ["StockQuantity"] = "42",
                ["IsActive"] = "true",
                ["CreatedAt"] = "2026-06-30T02:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
        Assert.Equal(42, product.StockQuantity);

        var movement = await context.ProductStockMovements.SingleAsync(movement => movement.ProductId == ids.ProductId);
        Assert.Equal("ManualAdjustment", movement.MovementType);
        Assert.Equal(42, movement.QuantityUnits);
        Assert.Null(movement.StockBeforeUnits);
        Assert.Equal(42, movement.StockAfterUnits);

        var detailsResponse = await client.GetAsync($"/Products/Details/{ids.ProductId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Contains("Estoque de produtos acabados", detailsHtml);
        Assert.Contains("42 un.", detailsHtml);
        Assert.Contains("Ajuste manual", detailsHtml);

        var indexResponse = await client.GetAsync("/Products");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Contains("42 un.", indexHtml);
    }

    [Fact]
    public async Task ProductEdit_RejectsNegativeStockQuantity_WithoutPersistingChange()
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
                ["StockQuantity"] = "-5",
                ["IsActive"] = "true",
                ["CreatedAt"] = "2026-06-30T02:00:00Z"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("O estoque de produtos acabados não pode ser negativo.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);

        Assert.Null(product.StockQuantity);
    }

    [Fact]
    public async Task ProductLowStockAlert_IsShown_OnDetailsAndIndexAndDashboard_WhenBelowMinimum()
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
                ["StockQuantity"] = "3",
                ["MinimumStockQuantity"] = "10",
                ["TargetMarginPercentage"] = "25",
                ["DefaultPackagingCost"] = "2.50",
                ["IsActive"] = "true",
                ["CreatedAt"] = "2026-06-30T02:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var detailsResponse = await client.GetAsync($"/Products/Details/{ids.ProductId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Estoque mínimo", detailsHtml);
        Assert.Contains("Baixo estoque", detailsHtml);
        Assert.Contains("Custo padrão de embalagem", detailsHtml);
        Assert.Contains("R$ 2,50", detailsHtml);

        var indexResponse = await client.GetAsync("/Products");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Baixo estoque", indexHtml);
        Assert.Contains("3 un.", indexHtml);
        Assert.Contains("10 un.", indexHtml);
        Assert.Contains("Margem alvo", indexHtml);
        Assert.Contains("25,0%", indexHtml);
        Assert.Contains("Custo padrão de embalagem", indexHtml);
        Assert.Contains("R$ 2,50", indexHtml);

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Alertas de estoque de produtos", dashboardHtml);
        Assert.Contains("Existem <strong>1</strong> produtos em baixo estoque.", dashboardHtml);
        Assert.Contains("Product A", dashboardHtml);
        Assert.Contains($"href=\"/Products/AdjustStock/{ids.ProductId}\"", dashboardHtml);
        Assert.Contains("Ajustar estoque", dashboardHtml);
        Assert.Contains("href=\"/Products?stockStatus=low\"", dashboardHtml);
        Assert.Contains("Ver produtos em baixo estoque", dashboardHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);

        Assert.Equal(3, product.StockQuantity);
        Assert.Equal(10, product.MinimumStockQuantity);
        Assert.Equal(25m, product.TargetMarginPercentage);
        Assert.Equal(2.50m, product.DefaultPackagingCost);
    }

    [Fact]
    public async Task ProductOutOfStockBadge_IsShown_OnDetailsAndIndex_WhenAlertConfiguredAndEmpty()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        await factory.SeedAsync(async context =>
        {
            var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
            product.StockQuantity = 0;
            product.MinimumStockQuantity = 10;
            await context.SaveChangesAsync();
        });
        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/Products/Details/{ids.ProductId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("<span class=\"badge bg-dark\">Sem estoque</span>", detailsHtml);
        Assert.DoesNotContain("Baixo estoque", detailsHtml);

        var indexResponse = await client.GetAsync("/Products");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("<span class=\"badge bg-dark\">Sem estoque</span>", indexHtml);

        // Out of stock is a strict subset of low stock, so the Dashboard alert still counts it.
        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Existem <strong>1</strong> produtos em baixo estoque.", dashboardHtml);
    }

    [Fact]
    public async Task ProductsIndex_ShowsOutOfStockCallout_OnlyWhenLowStockItemIsEmpty()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        await factory.SeedAsync(async context =>
        {
            var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
            product.StockQuantity = 3;
            product.MinimumStockQuantity = 10;
            await context.SaveChangesAsync();
        });
        using var client = factory.CreateTestClient();

        // Low but not empty: the header must not surface a "Sem estoque" callout.
        var lowHtml = WebUtility.HtmlDecode(await (await client.GetAsync("/Products")).Content.ReadAsStringAsync());
        Assert.Contains("Baixo estoque (1)", lowHtml);
        Assert.DoesNotContain("Sem estoque", lowHtml);

        // Drop the same low-stock product to zero: the header callout must now report 1 empty item.
        await factory.SeedAsync(async context =>
        {
            var product = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
            product.StockQuantity = 0;
            await context.SaveChangesAsync();
        });

        var emptyHtml = WebUtility.HtmlDecode(await (await client.GetAsync("/Products")).Content.ReadAsStringAsync());
        Assert.Contains("Baixo estoque (1)", emptyHtml);
        Assert.Contains("1 Sem estoque", emptyHtml);
    }

    [Fact]
    public async Task MaterialsIndex_ShowsOutOfStockCallout_OnlyWhenLowStockItemIsEmpty()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        await factory.SeedAsync(async context =>
        {
            var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
            material.CurrentStockGrams = 50m;
            material.MinimumStockGrams = 200m;
            await context.SaveChangesAsync();
        });
        using var client = factory.CreateTestClient();

        // Low but not empty: no "Sem estoque" callout in the header.
        var lowHtml = WebUtility.HtmlDecode(await (await client.GetAsync("/Materials")).Content.ReadAsStringAsync());
        Assert.Contains("Baixo estoque (1)", lowHtml);
        Assert.DoesNotContain("Sem estoque", lowHtml);

        // Empty the same low-stock material: the header callout must report 1 empty item.
        await factory.SeedAsync(async context =>
        {
            var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
            material.CurrentStockGrams = 0m;
            await context.SaveChangesAsync();
        });

        var emptyHtml = WebUtility.HtmlDecode(await (await client.GetAsync("/Materials")).Content.ReadAsStringAsync());
        Assert.Contains("Baixo estoque (1)", emptyHtml);
        Assert.Contains("1 Sem estoque", emptyHtml);
    }

    [Fact]
    public async Task ProductsIndex_FiltersLowStockProducts_WhenStockStatusLowRequested()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var okProductId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            var seededProduct = await context.Products.SingleAsync(product => product.Id == ids.ProductId);
            seededProduct.StockQuantity = 3;
            seededProduct.MinimumStockQuantity = 10;

            context.Products.Add(new Product
            {
                Id = okProductId,
                Name = "Product B",
                Sku = "E2E-PROD-B",
                Category = "Brindes",
                StockQuantity = 50,
                MinimumStockQuantity = 10,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var unfilteredResponse = await client.GetAsync("/Products");
        var unfilteredHtml = WebUtility.HtmlDecode(await unfilteredResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, unfilteredResponse.StatusCode);
        Assert.Contains("Baixo estoque (1)", unfilteredHtml);
        Assert.Contains("Product A", unfilteredHtml);
        Assert.Contains("Product B", unfilteredHtml);
        // Product B (stock 50, min 10) is above its minimum, so it must render the centralized
        // StockStatusPresentation "Ok" badge through the real view pipeline.
        Assert.Contains("<span class=\"badge bg-success\">Ok</span>", unfilteredHtml);

        var filteredResponse = await client.GetAsync("/Products?stockStatus=low");
        var filteredHtml = WebUtility.HtmlDecode(await filteredResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.Contains("Baixo estoque (1)", filteredHtml);
        Assert.Contains("Product A", filteredHtml);
        Assert.DoesNotContain("Product B", filteredHtml);
        // Row action links from the filtered queue must carry returnTo=lowStock so Details/Edit/Delete can round-trip back to it.
        Assert.Contains($"/Products/Details/{ids.ProductId}?returnTo=lowStock", filteredHtml);
        Assert.Contains($"/Products/Edit/{ids.ProductId}?returnTo=lowStock", filteredHtml);
        Assert.Contains($"/Products/Delete/{ids.ProductId}?returnTo=lowStock", filteredHtml);

        // Unfiltered rows must not carry a returnTo, since there is no filtered queue to return to.
        Assert.Contains($"/Products/Details/{ids.ProductId}\"", unfilteredHtml);

        // The "Estoque" (AdjustStock) row link must also round-trip returnTo=lowStock, like Details/Edit/Delete.
        Assert.Contains($"/Products/AdjustStock/{ids.ProductId}?returnTo=lowStock", filteredHtml);

        var detailsResponse = await client.GetAsync($"/Products/Details/{ids.ProductId}?returnTo=lowStock");
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("href=\"/Products?stockStatus=low\">Voltar para baixo estoque</a>", detailsHtml);
        Assert.Contains($"/Products/AdjustStock/{ids.ProductId}?returnTo=lowStock", detailsHtml);

        var adjustStockGetResponse = await client.GetAsync($"/Products/AdjustStock/{okProductId}?returnTo=lowStock");
        var adjustStockGetHtml = WebUtility.HtmlDecode(await adjustStockGetResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, adjustStockGetResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"lowStock\"", adjustStockGetHtml);

        var adjustStockToken = ExtractAntiForgeryToken(adjustStockGetHtml);
        var adjustStockPostResponse = await client.PostAsync(
            "/Products/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = adjustStockToken,
                ["ProductId"] = okProductId.ToString(),
                ["ProductName"] = "Product B",
                ["CurrentStockQuantity"] = "50",
                ["AdjustmentType"] = "Add",
                ["QuantityUnits"] = "5",
                ["returnTo"] = "lowStock"
            }));

        // Adjusting stock from the low-stock queue must land back on Details still carrying returnTo=lowStock.
        Assert.Equal(HttpStatusCode.Redirect, adjustStockPostResponse.StatusCode);
        Assert.Equal($"/Products/Details/{okProductId}?returnTo=lowStock", adjustStockPostResponse.Headers.Location?.ToString());

        var deleteResponse = await client.GetAsync($"/Products/Delete/{okProductId}?returnTo=lowStock");
        var deleteHtml = WebUtility.HtmlDecode(await deleteResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"lowStock\"", deleteHtml);

        var deleteToken = ExtractAntiForgeryToken(deleteHtml);
        var deletePostResponse = await client.PostAsync(
            $"/Products/Delete/{okProductId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = deleteToken,
                ["Id"] = okProductId.ToString(),
                ["returnTo"] = "lowStock"
            }));

        // Deleting from the low-stock queue must return to the filtered queue, not the unfiltered Index.
        Assert.Equal(HttpStatusCode.Redirect, deletePostResponse.StatusCode);
        Assert.Equal("/Products?stockStatus=low", deletePostResponse.Headers.Location?.ToString());
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
    public async Task PrinterEdit_PersistsCostPerHour_AndShowsOnPrintersList()
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
                ["Name"] = "Printer A",
                ["Brand"] = "E2E",
                ["Model"] = "Model A",
                ["CostPerHour"] = "2.50",
                ["Notes"] = "Custo operacional",
                ["CreatedAt"] = "2026-06-30T02:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printer = await context.Printers.SingleAsync(printer => printer.Id == ids.PrinterId);
        Assert.Equal(2.50m, printer.CostPerHour);

        var indexResponse = await client.GetAsync("/Printers");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Custo por hora", indexHtml);
        Assert.Contains("2,50", indexHtml);
    }

    [Fact]
    public async Task PrinterEdit_RejectsNegativeCostPerHour_WithoutPersistingChange()
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
                ["Name"] = "Printer A",
                ["Brand"] = "E2E",
                ["Model"] = "Model A",
                ["CostPerHour"] = "-1.00",
                ["Notes"] = "Custo negativo",
                ["CreatedAt"] = "2026-06-30T02:00:00Z"
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("O custo por hora não pode ser negativo.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printer = await context.Printers.SingleAsync(printer => printer.Id == ids.PrinterId);
        Assert.Null(printer.CostPerHour);
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
    public async Task MaterialsIndex_ShowsTotalInventoryValue_SummedAcrossMaterials()
    {
        using var factory = new ThreeDManagerWebFactory();
        await SeedCommonAsync(factory); // PLA Preto: CostPerKg 80 x 1000 g = R$ 80,00
        await factory.SeedAsync(async context =>
        {
            context.Materials.Add(new Material
            {
                Id = Guid.NewGuid(),
                Name = "PETG Azul",
                Type = "PETG",
                Brand = "E2E",
                Color = "Blue",
                CostPerKg = 100m,
                CurrentStockGrams = 500m, // 100/1000 x 500 = R$ 50,00
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        });
        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/Materials");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Valor total em estoque", indexHtml);
        // R$ 80,00 (PLA Preto) + R$ 50,00 (PETG Azul) = R$ 130,00.
        Assert.Contains("R$ 130,00", indexHtml);
        // Per-row stock value column also surfaces each material's individual value.
        Assert.Contains("Valor em estoque", indexHtml);
        Assert.Contains("R$ 80,00", indexHtml); // PLA Preto: 80/1000 x 1000 g
        Assert.Contains("R$ 50,00", indexHtml); // PETG Azul: 100/1000 x 500 g
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
        Assert.Contains("href=\"/Materials?stockStatus=low\"", dashboardHtml);
        Assert.Contains("Ver materiais em baixo estoque", dashboardHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);

        Assert.Equal(120m, material.CurrentStockGrams);
        Assert.Equal(200m, material.MinimumStockGrams);
    }

    [Fact]
    public async Task Dashboard_DistinguishesOutOfStockMaterial_FromLowStock_WhenStockIsZero()
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
                ["Name"] = "PLA Esgotado",
                ["Type"] = "PLA",
                ["Brand"] = "E2E",
                ["Color"] = "Black",
                ["CostPerKg"] = "80.00",
                ["CurrentStockGrams"] = "0.00",
                ["MinimumStockGrams"] = "200.00",
                ["CreatedAt"] = "2026-06-19T02:00:00Z"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Existem <strong>1</strong> materiais em baixo estoque.", dashboardHtml);
        Assert.Contains("<strong>1</strong> sem estoque no momento.", dashboardHtml);
        Assert.Contains("Sem estoque", dashboardHtml);
        Assert.Contains("badge bg-dark", dashboardHtml);
    }

    [Fact]
    public async Task MaterialsIndex_FiltersLowStockMaterials_WhenStockStatusLowRequested()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory, stockGrams: 120m);

        var okMaterialId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            var seededMaterial = await context.Materials.SingleAsync(material => material.Id == ids.MaterialId);
            seededMaterial.MinimumStockGrams = 200m;

            context.Materials.Add(new Material
            {
                Id = okMaterialId,
                Name = "PETG Branco",
                Type = "PETG",
                Brand = "E2E",
                Color = "White",
                CostPerKg = 90m,
                CurrentStockGrams = 1000m,
                MinimumStockGrams = 200m,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var unfilteredResponse = await client.GetAsync("/Materials");
        var unfilteredHtml = WebUtility.HtmlDecode(await unfilteredResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, unfilteredResponse.StatusCode);
        Assert.Contains("Baixo estoque (1)", unfilteredHtml);
        Assert.Contains("PLA Preto", unfilteredHtml);
        Assert.Contains("PETG Branco", unfilteredHtml);
        // PETG Branco (1000 g, min 200 g) is above its minimum, so it must render the centralized
        // StockStatusPresentation "Ok" badge through the real view pipeline.
        Assert.Contains("<span class=\"badge bg-success\">Ok</span>", unfilteredHtml);

        var filteredResponse = await client.GetAsync("/Materials?stockStatus=low");
        var filteredHtml = WebUtility.HtmlDecode(await filteredResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.Contains("Baixo estoque (1)", filteredHtml);
        Assert.Contains("PLA Preto", filteredHtml);
        Assert.DoesNotContain("PETG Branco", filteredHtml);
        // Row action links from the filtered queue must carry returnTo=lowStock so Details/Edit/Delete can round-trip back to it.
        Assert.Contains($"/Materials/Details/{ids.MaterialId}?returnTo=lowStock", filteredHtml);
        Assert.Contains($"/Materials/Edit/{ids.MaterialId}?returnTo=lowStock", filteredHtml);
        Assert.Contains($"/Materials/Delete/{ids.MaterialId}?returnTo=lowStock", filteredHtml);

        // Unfiltered rows must not carry a returnTo, since there is no filtered queue to return to.
        Assert.Contains($"/Materials/Details/{ids.MaterialId}\"", unfilteredHtml);

        // The "Estoque" (AdjustStock) row link must also round-trip returnTo=lowStock, like Details/Edit/Delete.
        Assert.Contains($"/Materials/AdjustStock/{ids.MaterialId}?returnTo=lowStock", filteredHtml);

        var detailsResponse = await client.GetAsync($"/Materials/Details/{ids.MaterialId}?returnTo=lowStock");
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("href=\"/Materials?stockStatus=low\">Voltar para baixo estoque</a>", detailsHtml);
        Assert.Contains($"/Materials/AdjustStock/{ids.MaterialId}?returnTo=lowStock", detailsHtml);

        var adjustStockGetResponse = await client.GetAsync($"/Materials/AdjustStock/{okMaterialId}?returnTo=lowStock");
        var adjustStockGetHtml = WebUtility.HtmlDecode(await adjustStockGetResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, adjustStockGetResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"lowStock\"", adjustStockGetHtml);

        var adjustStockToken = ExtractAntiForgeryToken(adjustStockGetHtml);
        var adjustStockPostResponse = await client.PostAsync(
            "/Materials/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = adjustStockToken,
                ["MaterialId"] = okMaterialId.ToString(),
                ["MaterialName"] = "PETG Branco",
                ["CurrentStockGrams"] = "1000",
                ["AdjustmentType"] = "Add",
                ["QuantityGrams"] = "50",
                ["returnTo"] = "lowStock"
            }));

        // Adjusting stock from the low-stock queue must land back on Details still carrying returnTo=lowStock.
        Assert.Equal(HttpStatusCode.Redirect, adjustStockPostResponse.StatusCode);
        Assert.Equal($"/Materials/Details/{okMaterialId}?returnTo=lowStock", adjustStockPostResponse.Headers.Location?.ToString());

        var deleteResponse = await client.GetAsync($"/Materials/Delete/{okMaterialId}?returnTo=lowStock");
        var deleteHtml = WebUtility.HtmlDecode(await deleteResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"lowStock\"", deleteHtml);

        var deleteToken = ExtractAntiForgeryToken(deleteHtml);
        var deletePostResponse = await client.PostAsync(
            $"/Materials/Delete/{okMaterialId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = deleteToken,
                ["Id"] = okMaterialId.ToString(),
                ["returnTo"] = "lowStock"
            }));

        // Deleting from the low-stock queue must return to the filtered queue, not the unfiltered Index.
        Assert.Equal(HttpStatusCode.Redirect, deletePostResponse.StatusCode);
        Assert.Equal("/Materials?stockStatus=low", deletePostResponse.Headers.Location?.ToString());
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
    public async Task EditPrintJob_ToFailed_RequiresFailureReason_WithoutChangingStatus()
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
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Failed",
                ["FailureReason"] = ""
            }));

        var responseHtml = WebUtility.HtmlDecode(await postResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Contains("Informe o motivo da falha para marcar a produção como falha.", responseHtml);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.Id == printJobId);

        Assert.Equal("Imported", printJob.Status);
        Assert.Null(printJob.FailureReason);
    }

    [Fact]
    public async Task EditPrintJob_ToFailed_WithReason_PersistsAndShowsOnDetails()
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
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Failed",
                ["FailureReason"] = "Destacamento da primeira camada"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/PrintJobs/Details/{printJobId}", postResponse.Headers.Location?.ToString());

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.Id == printJobId);

        Assert.Equal("Failed", printJob.Status);
        Assert.Equal("Destacamento da primeira camada", printJob.FailureReason);

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Motivo da falha", detailsHtml);
        Assert.Contains("Destacamento da primeira camada", detailsHtml);

        var indexResponse = await client.GetAsync("/PrintJobs");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Motivo: Destacamento da primeira camada", indexHtml);

        var reEditGetResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        var reEditToken = ExtractAntiForgeryToken(await reEditGetResponse.Content.ReadAsStringAsync());
        var reEditPostResponse = await client.PostAsync(
            $"/PrintJobs/Edit/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = reEditToken,
                ["Id"] = printJobId.ToString(),
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["PrintImportId"] = "",
                ["SourceFileName"] = "legacy.gcode",
                ["CreatedAt"] = "2026-06-19T02:00:00Z",
                ["FilamentUsedGrams"] = "12.45",
                ["FilamentUsedMeters"] = "1.23",
                ["EstimatedTimeMinutes"] = "60",
                ["ActualTimeMinutes"] = "",
                ["ReportedCost"] = "2.50",
                ["Status"] = "Completed",
                ["FailureReason"] = "Destacamento da primeira camada"
            }));

        Assert.Equal(HttpStatusCode.Redirect, reEditPostResponse.StatusCode);

        using var reVerifyScope = factory.Services.CreateScope();
        var reVerifyContext = reVerifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recoveredPrintJob = await reVerifyContext.PrintJobs.SingleAsync(printJob => printJob.Id == printJobId);

        Assert.Equal("Completed", recoveredPrintJob.Status);
        Assert.Equal("Destacamento da primeira camada", recoveredPrintJob.FailureReason);

        var recoveredDetailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var recoveredDetailsHtml = WebUtility.HtmlDecode(await recoveredDetailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, recoveredDetailsResponse.StatusCode);
        Assert.DoesNotContain("Motivo da falha", recoveredDetailsHtml);
        Assert.DoesNotContain("Destacamento da primeira camada", recoveredDetailsHtml);
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
    public async Task CreatePrintJob_FromImport_ShowsDerivedMachineAndTotalCost_FromPrinterCostPerHour()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.TargetMarginPercentage = 50.0m;
            product.DefaultPackagingCost = 2.50m;

            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "machine-cost.gcode",
                FileType = "gcode",
                ParsedDataJson = """
                {"filamentUsedGrams":12.45,"filamentUsedMeters":1.23,"estimatedTimeMinutes":120,"reportedCost":2.5,"materialType":"PLA","warnings":[]}
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
        Assert.Contains("Custo por hora da impressora", html);
        Assert.Contains("Custo de máquina estimado", html);
        Assert.Contains("Custo total estimado da produção", html);
        // Seeded printer CostPerHour = 5.00 -> invariant string in the option data attribute.
        Assert.Contains("data-cost-per-hour=\"5.00\"", html);
        Assert.Contains("actualTimeMinutes.addEventListener(\"input\", updateMachineCostHints)", html);
        Assert.Contains("printerSelect.addEventListener(\"change\", updateTotalProductionCostHint)", html);
        Assert.Contains("materialCost + machineCost + packagingAmount", html);
        Assert.Contains("id=\"PackagingCost\"", html);
        Assert.Contains("packagingCost.addEventListener(\"input\", updateTotalProductionCostHint)", html);
        Assert.Contains("Preço de venda sugerido", html);
        Assert.Contains("Margem alvo do produto", html);
        // Seeded product TargetMarginPercentage = 50.0 -> invariant string in the option data attribute.
        Assert.Contains("data-target-margin-percentage=\"50.0\"", html);
        // Seeded product DefaultPackagingCost = 2.50 -> invariant string in the option data attribute.
        Assert.Contains("data-default-packaging-cost=\"2.50\"", html);
        Assert.Contains("applyDefaultPackagingCost();", html);
        Assert.Contains("productSelect.addEventListener(\"change\", function ()", html);
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
    public async Task PrintJobsIndex_ShowsCostDifference_FromReportedAndCalculatedCosts()
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
                SourceFileName = "cost-difference.gcode",
                Status = PrintJobStatus.Completed,
                ReportedCost = 3.45m,
                CalculatedMaterialCost = 1.11m,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/PrintJobs");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Diferença de custo", indexHtml);
        // ReportedCost 3.45 - CalculatedMaterialCost 1.11 = 2.34 formatted as currency (C).
        Assert.Contains("2,34", indexHtml);
        Assert.Contains("acima do calculado", indexHtml);
        Assert.Contains("text-danger", indexHtml);

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("2,34", detailsHtml);
        Assert.Contains("acima do calculado", detailsHtml);
        Assert.Contains("text-danger", detailsHtml);
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
    public async Task PrintJobsDetails_ShowsEstimatedMachineCost_FromPrinterCostPerHourAndTime()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "machine-cost.gcode",
                EstimatedTimeMinutes = 120,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Custo por hora da impressora", detailsHtml);
        Assert.Contains("Custo de máquina estimado", detailsHtml);
        // 120 min / 60 * R$ 5,00/h = R$ 10,00; no actual time -> falls back to estimated time.
        Assert.Contains("R$ 10,00", detailsHtml);
        Assert.Contains("tempo estimado", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_ShowsEstimatedTotalProductionCost_FromMaterialAndMachineCosts()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "total-cost.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 1.00m,
                PackagingCost = 2.50m,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Custo total estimado da produção", detailsHtml);
        // material R$ 1,00 + máquina (120/60 * R$ 5,00/h = R$ 10,00) + embalagem R$ 2,50 = R$ 13,50.
        Assert.Contains("R$ 13,50", detailsHtml);
        Assert.Contains("embalagem", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_ShowsEstimatedCostPerUnit_WhenJobProducesMultipleUnits()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "cost-per-unit.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 2.00m,
                PackagingCost = 2.00m,
                UnitsProduced = 4,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Custo por unidade", detailsHtml);
        // total cost R$ 14,00 (material R$ 2,00 + máquina R$ 10,00 + embalagem R$ 2,00) ÷ 4 unidades = R$ 3,50.
        Assert.Contains("R$ 3,50", detailsHtml);
        Assert.Contains("÷ 4 unidades", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_HidesCostPerUnit_ForSingleUnitJob()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "single-unit.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 2.00m,
                PackagingCost = 2.00m,
                UnitsProduced = 1,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.DoesNotContain("Custo por unidade", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_ComputesMarginAndSuggestedPricePerUnit_ForMultiUnitJob()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.SalePrice = 20.00m;
            product.TargetMarginPercentage = 50.0m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "per-unit-margin.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 2.00m,
                PackagingCost = 2.00m,
                UnitsProduced = 4,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        // total cost R$ 14,00 (material R$ 2,00 + máquina R$ 10,00 + embalagem R$ 2,00) ÷ 4 unidades = R$ 3,50 per unit.
        // margin compares the per-unit sale price against the per-unit cost: R$ 20,00 - R$ 3,50 = R$ 16,50 (82,5%).
        Assert.Contains("Margem estimada", detailsHtml);
        Assert.Contains("R$ 16,50", detailsHtml);
        Assert.Contains("82,5%", detailsHtml);
        Assert.Contains("custo por unidade R$ 3,50", detailsHtml);
        // suggested price is per-unit as well: R$ 3,50 × 1,5 = R$ 5,25 (not the whole-batch R$ 21,00).
        Assert.Contains("R$ 5,25", detailsHtml);
        Assert.DoesNotContain("R$ 21,00", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsIndex_ComputesMarginAndSuggestedPricePerUnit_ForMultiUnitJob()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.SalePrice = 20.00m;
            product.TargetMarginPercentage = 50.0m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "index-per-unit-margin.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 2.00m,
                PackagingCost = 2.00m,
                UnitsProduced = 4,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/PrintJobs");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        // whole-batch cost R$ 14,00 ÷ 4 unidades = R$ 3,50 per unit.
        Assert.Contains("R$ 14,00", indexHtml);
        // margin = R$ 20,00 - R$ 3,50 = R$ 16,50 (82,5%); suggested price = R$ 3,50 × 1,5 = R$ 5,25.
        Assert.Contains("R$ 16,50", indexHtml);
        Assert.Contains("82,5%", indexHtml);
        Assert.Contains("R$ 5,25", indexHtml);
        Assert.DoesNotContain("R$ 21,00", indexHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_ShowsEstimatedMargin_FromProductSalePriceAndTotalProductionCost()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.SalePrice = 20.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "margin.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 1.00m,
                PackagingCost = 2.50m,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Margem estimada", detailsHtml);
        // total cost R$ 13,50 (material R$ 1,00 + máquina R$ 10,00 + embalagem R$ 2,50); margin = R$ 20,00 - R$ 13,50 = R$ 6,50 (32,5%).
        Assert.Contains("R$ 6,50", detailsHtml);
        Assert.Contains("32,5%", detailsHtml);
        Assert.Contains("lucro estimado", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_ShowsSuggestedSalePrice_FromProductTargetMarginPercentageAndTotalProductionCost()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.TargetMarginPercentage = 50.0m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "suggested-price.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 1.00m,
                PackagingCost = 2.50m,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Preço de venda sugerido", detailsHtml);
        // total cost R$ 13,50 (material R$ 1,00 + máquina R$ 10,00 + embalagem R$ 2,50); suggested price = R$ 13,50 × 1,5 = R$ 20,25.
        Assert.Contains("R$ 20,25", detailsHtml);
        Assert.Contains("margem alvo 50,0%", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_ShowsMissingTargetMarginHint_ForSuggestedPrice_WhenProductHasNoTargetMargin()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "no-target-margin.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 1.00m,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Preço de venda sugerido", detailsHtml);
        Assert.Contains("Produto sem margem alvo informada", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_ShowsMissingSalePriceHint_ForMarginWhenProductHasNoSalePrice()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "no-sale-price.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 1.00m,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Margem estimada", detailsHtml);
        Assert.Contains("Produto sem preço de venda informado", detailsHtml);
    }

    [Fact]
    public async Task PrintJobsIndex_ShowsEstimatedMachineCost_FromPrinterCostPerHourAndTime()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "index-machine-cost.gcode",
                EstimatedTimeMinutes = 120,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/PrintJobs");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Custo de máquina estimado", indexHtml);
        // 120 min / 60 * R$ 5,00/h = R$ 10,00; no actual time -> falls back to estimated time.
        Assert.Contains("R$ 10,00", indexHtml);
        Assert.Contains("tempo estimado", indexHtml);
    }

    [Fact]
    public async Task PrintJobsIndex_ShowsEstimatedTotalProductionCost_FromMaterialAndMachineCosts()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.SalePrice = 20.00m;
            product.TargetMarginPercentage = 50.0m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "index-total-cost.gcode",
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 1.00m,
                PackagingCost = 2.50m,
                Status = PrintJobStatus.Completed,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexResponse = await client.GetAsync("/PrintJobs");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Custo total estimado da produção", indexHtml);
        // material R$ 1,00 + máquina (120/60 * R$ 5,00/h = R$ 10,00) + embalagem R$ 2,50 = R$ 13,50.
        Assert.Contains("R$ 13,50", indexHtml);
        Assert.Contains("+ embalagem R$ 2,50", indexHtml);
        // margin = R$ 20,00 - R$ 13,50 = R$ 6,50 (32.5%).
        Assert.Contains("Margem estimada", indexHtml);
        Assert.Contains("R$ 6,50", indexHtml);
        Assert.Contains("32,5%", indexHtml);
        Assert.Contains("lucro estimado", indexHtml);
        // suggested sale price = R$ 13,50 * (1 + 50/100) = R$ 20,25.
        Assert.Contains("Preço de venda sugerido", indexHtml);
        Assert.Contains("R$ 20,25", indexHtml);
        Assert.Contains("margem alvo 50,0%", indexHtml);
    }

    [Fact]
    public async Task EditPrintJob_ShowsDerivedMaterialCostPerGram_FromMaterialCostPerKg()
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
                SourceFileName = "edit-cost-per-gram.gcode",
                Status = PrintJobStatus.Planned,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var editResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        var editHtml = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        Assert.Contains("Custo por grama do material", editHtml);
        Assert.Contains("Custo estimado do material", editHtml);
        // Seeded material CostPerKg = 80 -> 80 / 1000 = 0.08 (invariant) in the option data attribute.
        Assert.Contains("data-cost-per-gram=\"0.08\"", editHtml);
        Assert.Contains("filamentUsedGrams.addEventListener(\"input\", updateCostHints)", editHtml);
    }

    [Fact]
    public async Task EditPrintJob_ShowsDerivedMachineCost_FromPrinterCostPerHourAndTime()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "edit-machine-cost.gcode",
                EstimatedTimeMinutes = 120,
                Status = PrintJobStatus.Planned,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var editResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        var editHtml = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        Assert.Contains("Custo por hora da impressora", editHtml);
        Assert.Contains("Custo de máquina estimado", editHtml);
        // Seeded printer CostPerHour = 5.00 -> invariant string in the option data attribute.
        Assert.Contains("data-cost-per-hour=\"5.00\"", editHtml);
        Assert.Contains("actualTimeMinutes.addEventListener(\"input\", updateMachineCostHints)", editHtml);
    }

    [Fact]
    public async Task EditPrintJob_ShowsLiveEstimatedTotalProductionCost_FromMaterialAndMachineCosts()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var printJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.TargetMarginPercentage = 50.0m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = printJobId,
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "edit-total-cost.gcode",
                FilamentUsedGrams = 12.50m,
                EstimatedTimeMinutes = 120,
                PackagingCost = 2.50m,
                Status = PrintJobStatus.Planned,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var editResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        var editHtml = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        Assert.Contains("Custo total estimado da produção", editHtml);
        Assert.Contains("Preço de venda sugerido", editHtml);
        Assert.Contains("data-cost-per-gram=\"0.08\"", editHtml);
        Assert.Contains("data-cost-per-hour=\"5.00\"", editHtml);
        Assert.Contains("data-target-margin-percentage=\"50.0\"", editHtml);
        Assert.Contains("materialCost + machineCost + packagingAmount", editHtml);
        Assert.Contains("totalProductionCost * (1 + (targetMarginPercentage / 100))", editHtml);
        Assert.Contains("id=\"PackagingCost\"", editHtml);
        Assert.Contains("productSelect.addEventListener(\"change\", updateTotalProductionCostHint)", editHtml);
        Assert.Contains("actualTimeMinutes.addEventListener(\"input\", updateTotalProductionCostHint)", editHtml);
        Assert.Contains("packagingCost.addEventListener(\"input\", updateTotalProductionCostHint)", editHtml);
    }

    [Fact]
    public async Task Dashboard_ShowsTotalCostDifference_FromReportedAndCalculatedCosts()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-cost-a.gcode",
                    Status = PrintJobStatus.Completed,
                    ReportedCost = 12.00m,
                    CalculatedMaterialCost = 5.00m,
                    CreatedAt = DateTime.UtcNow.AddMinutes(2)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-cost-b.gcode",
                    Status = PrintJobStatus.Completed,
                    ReportedCost = 8.00m,
                    CalculatedMaterialCost = 2.00m,
                    CreatedAt = DateTime.UtcNow.AddMinutes(1)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        // The seeded jobs carry both reported and calculated material costs, so the cost card must
        // surface the derived "Diferença de custo" (reported minus calculated) next to the existing
        // reported/calculated totals. Currency formatting is culture-dependent (NBSP separator) and
        // the in-memory totals accumulate across the shared test run, so scope the assertion to the
        // cost card and verify the difference line renders a numeric value rather than an exact amount.
        var costCard = dashboardHtml[dashboardHtml.IndexOf("Custo informado", StringComparison.Ordinal)..];

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Diferença de custo:", costCard);
        Assert.Matches(@"Diferença de custo:[^<]*\d", costCard);
    }

    [Fact]
    public async Task Dashboard_ShowsTotalEstimatedMachineAndProductionCost_FromPrinterCostPerHourAndTime()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.SalePrice = 20.00m;

            context.PrintJobs.Add(new PrintJob
            {
                Id = Guid.NewGuid(),
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "dashboard-machine-cost.gcode",
                Status = PrintJobStatus.Completed,
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 1.00m,
                PackagingCost = 2.50m,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        // The cost card must surface a total estimated machine cost (sum of per-job
        // (ActualTimeMinutes ?? EstimatedTimeMinutes) / 60 * printer CostPerHour), a combined
        // total estimated production cost (material + machine + packaging), and an aggregate
        // estimated margin (sum of product SalePrice - total estimated cost, only for productions
        // where both a complete cost total and a linked product sale price exist). Currency
        // formatting is culture dependent (NBSP separator) and the in-memory totals accumulate
        // across the shared test run, so scope to the cost card and assert the labels render a
        // numeric value.
        var costCard = dashboardHtml[dashboardHtml.IndexOf("Custo informado", StringComparison.Ordinal)..];

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Máquina estimada:", costCard);
        Assert.Matches(@"Máquina estimada:[^<]*\d", costCard);
        Assert.Contains("Embalagem:", costCard);
        Assert.Matches(@"Embalagem:[^<]*\d", costCard);
        Assert.Contains("Custo total estimado:", costCard);
        Assert.Matches(@"Custo total estimado:[^<]*\d", costCard);
        Assert.Contains("Margem estimada:", costCard);
        Assert.Matches(@"Margem estimada:(.|\n){0,60}?\d", costCard);
        Assert.Matches(@"\(\d+ produções\)", costCard);
    }

    [Fact]
    public async Task Dashboard_ShowsLocalizedStatusLabels_InRecentPrintJobsTable()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var completedPrintJobId = Guid.NewGuid();
        var canceledPrintJobId = Guid.NewGuid();
        var failedPrintJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 6.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.TargetMarginPercentage = 50.0m;

            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = completedPrintJobId,
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-completed.gcode",
                    Status = PrintJobStatus.Completed,
                    EstimatedTimeMinutes = 120,
                    CalculatedMaterialCost = 1.00m,
                    PackagingCost = 2.50m,
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
                },
                new PrintJob
                {
                    Id = failedPrintJobId,
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-failed.gcode",
                    Status = PrintJobStatus.Failed,
                    FailureReason = "Empenamento nas bordas durante teste de dashboard",
                    CreatedAt = DateTime.UtcNow.AddMinutes(8)
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
        Assert.Contains("Custo calculado do material:", recentPrintJobsSection);
        Assert.Contains("Custo de máquina estimado:", recentPrintJobsSection);
        Assert.Contains("Embalagem:", recentPrintJobsSection);
        Assert.Contains("Custo total estimado:", recentPrintJobsSection);
        Assert.Contains("Preço de venda sugerido:", recentPrintJobsSection);
        // Completed job: (1.00 material + (120/60)*6.00 machine + 2.50 packaging) = 15.50 total; 15.50 * 1.5 (50% target margin) = 23.25.
        Assert.Contains("R$ 23,25", recentPrintJobsSection);
        Assert.Contains("(margem alvo 50,0%)", recentPrintJobsSection);
        Assert.DoesNotContain("<th>Tempo</th>", recentPrintJobsSection);
        Assert.DoesNotContain("<th>Custo</th>", recentPrintJobsSection);
        Assert.DoesNotContain("Arquivo:", recentPrintJobsSection);
        Assert.DoesNotContain("Material:", recentPrintJobsSection);
        Assert.Contains("Concluída", dashboardHtml);
        Assert.Contains("Cancelada", dashboardHtml);
        Assert.DoesNotContain("<th>Status</th>", recentPrintJobsSection);
        Assert.DoesNotContain(">Completed<", dashboardHtml);
        Assert.DoesNotContain(">Canceled<", dashboardHtml);
        Assert.Contains("dashboard-failed.gcode", dashboardHtml);
        Assert.Contains("Falhou", dashboardHtml);
        Assert.Contains("Motivo: Empenamento nas bordas durante teste de dashboard", dashboardHtml);
    }

    [Fact]
    public async Task Dashboard_UsesPerUnitCostBasis_ForMultiUnitJobMarginAndSuggestedPrice()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        await factory.SeedAsync(async context =>
        {
            var printer = await context.Printers.FirstAsync(printer => printer.Id == ids.PrinterId);
            printer.CostPerHour = 5.00m;

            var product = await context.Products.FirstAsync(product => product.Id == ids.ProductId);
            product.SalePrice = 20.00m;
            product.TargetMarginPercentage = 50.0m;

            // Batch of 4 units: material 2,00 + máquina (120/60)*5,00 = 10,00 + embalagem 2,00
            // = 14,00 total → 3,50 por unidade.
            context.PrintJobs.Add(new PrintJob
            {
                Id = Guid.NewGuid(),
                ProductId = ids.ProductId,
                PrinterId = ids.PrinterId,
                MaterialId = ids.MaterialId,
                SourceFileName = "dashboard-batch-4.gcode",
                Status = PrintJobStatus.Completed,
                EstimatedTimeMinutes = 120,
                CalculatedMaterialCost = 2.00m,
                PackagingCost = 2.00m,
                UnitsProduced = 4,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);

        // Portfolio margin is per-batch revenue (SalePrice 20,00 × 4 units = 80,00) minus the
        // whole-batch cost 14,00 = 66,00. The old per-unit-vs-whole-batch form gave 20,00 − 14,00
        // = 6,00, so the buggy value must be gone. Only this one job carries a complete cost + sale
        // price, so the aggregate is deterministic within the isolated test database.
        var costCard = dashboardHtml[dashboardHtml.IndexOf("Custo informado", StringComparison.Ordinal)..];
        Assert.Contains("Margem estimada:", costCard);
        Assert.Contains("R$ 66,00", costCard);
        Assert.DoesNotContain("R$ 6,00", costCard);
        Assert.Contains("(1 produções)", costCard);

        // Suggested price is per-unit: 3,50 × 1,5 (50% target margin) = 5,25, NOT the old
        // whole-batch 14,00 × 1,5 = 21,00. The caption flags the per-unit basis for batch jobs.
        var recentPrintJobsSection = dashboardHtml[dashboardHtml.IndexOf("Últimas produções", StringComparison.Ordinal)..];
        Assert.Contains("dashboard-batch-4.gcode", recentPrintJobsSection);
        Assert.Contains("Preço de venda sugerido:", recentPrintJobsSection);
        Assert.Contains("R$ 5,25", recentPrintJobsSection);
        Assert.Contains("por unidade, margem alvo 50,0%", recentPrintJobsSection);
        Assert.DoesNotContain("R$ 21,00", recentPrintJobsSection);
    }

    [Fact]
    public async Task Dashboard_NormalizesProductionStatusCounts()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-lower-completed.gcode",
                    Status = "completed",
                    CreatedAt = DateTime.UtcNow.AddMinutes(10)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-lower-failed.gcode",
                    Status = "failed",
                    CreatedAt = DateTime.UtcNow.AddMinutes(9)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-lower-planned.gcode",
                    Status = "planned",
                    CreatedAt = DateTime.UtcNow.AddMinutes(8)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "dashboard-lower-imported.gcode",
                    Status = "imported",
                    CreatedAt = DateTime.UtcNow.AddMinutes(7)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.True(ExtractDashboardCardCount(dashboardHtml, "Produções totais") >= 4);
        Assert.True(ExtractDashboardCardCount(dashboardHtml, "Concluídas") >= 1);
        Assert.True(ExtractDashboardCardCount(dashboardHtml, "Falhas") >= 1);
        Assert.True(ExtractDashboardCardCount(dashboardHtml, "Planejadas/importadas") >= 2);
    }

    [Fact]
    public async Task Dashboard_ShowsPrinterQueue_ForPlannedAndImportedProductionsOnly()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "queue-imported.gcode",
                    Status = PrintJobStatus.Imported,
                    EstimatedTimeMinutes = 60,
                    CreatedAt = DateTime.UtcNow.AddMinutes(10)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "queue-planned.gcode",
                    Status = PrintJobStatus.Planned,
                    EstimatedTimeMinutes = 90,
                    CreatedAt = DateTime.UtcNow.AddMinutes(9)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "queue-long-planned.gcode",
                    Status = PrintJobStatus.Planned,
                    EstimatedTimeMinutes = 330,
                    CreatedAt = DateTime.UtcNow.AddMinutes(8)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "queue-completed.gcode",
                    Status = PrintJobStatus.Completed,
                    EstimatedTimeMinutes = 120,
                    CreatedAt = DateTime.UtcNow.AddMinutes(7)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "queue-failed.gcode",
                    Status = PrintJobStatus.Failed,
                    FailureReason = "Falha de teste da fila",
                    EstimatedTimeMinutes = 30,
                    CreatedAt = DateTime.UtcNow.AddMinutes(6)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var beforeRequest = DateTime.UtcNow;
        var dashboardResponse = await client.GetAsync("/Dashboard");
        var afterRequest = DateTime.UtcNow;
        var dashboardHtml = WebUtility.HtmlDecode(await dashboardResponse.Content.ReadAsStringAsync());

        var queueSectionStart = dashboardHtml.IndexOf("Fila de produção por impressora", StringComparison.Ordinal);
        var queueSectionEnd = dashboardHtml.IndexOf("Movimentações de estoque recentes", StringComparison.Ordinal);
        var queueSection = dashboardHtml[queueSectionStart..queueSectionEnd];

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("Printer A", queueSection);
        // Only Imported/Planned jobs belong in the queue: 3 jobs, 480 minutes = 8h 0min.
        Assert.Matches(@"<td>\s*<a href=""/PrintJobs\?printerId=" + Regex.Escape(ids.PrinterId.ToString()) + @""">Printer A</a>\s*</td>\s*<td>3</td>\s*<td>\s*8h 0min\s*<span class=""badge bg-warning text-dark ms-1"">Fila alta</span>", queueSection);
        Assert.DoesNotContain("queue-completed", queueSection);
        Assert.DoesNotContain("queue-failed", queueSection);

        Assert.Contains("Previsão de fila livre", queueSection);
        Assert.Contains("title=\"Estimativa assume impressão sequencial e ininterrupta a partir de agora; não considera paradas, manutenção ou impressões simultâneas.\"", queueSection);
        var clearAtMatch = Regex.Match(queueSection, @"<td>(\d{2}/\d{2}/\d{4} \d{2}:\d{2})</td>");
        Assert.True(clearAtMatch.Success, "Expected the estimated queue clear-time cell to render.");
        var renderedClearAt = DateTime.ParseExact(clearAtMatch.Groups[1].Value, "dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        Assert.InRange(
            renderedClearAt,
            beforeRequest.AddMinutes(480).ToLocalTime().AddMinutes(-1),
            afterRequest.AddMinutes(480).ToLocalTime().AddMinutes(1));
    }

    [Fact]
    public async Task PrintersIndex_ShowsQueueSummary_ForPlannedAndImportedProductionsOnly()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "printers-queue-imported.gcode",
                    Status = PrintJobStatus.Imported,
                    EstimatedTimeMinutes = 60,
                    CreatedAt = DateTime.UtcNow.AddMinutes(10)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "printers-queue-planned.gcode",
                    Status = PrintJobStatus.Planned,
                    EstimatedTimeMinutes = 90,
                    CreatedAt = DateTime.UtcNow.AddMinutes(9)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "printers-queue-long-planned.gcode",
                    Status = PrintJobStatus.Planned,
                    EstimatedTimeMinutes = 330,
                    CreatedAt = DateTime.UtcNow.AddMinutes(8)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "printers-queue-completed.gcode",
                    Status = PrintJobStatus.Completed,
                    EstimatedTimeMinutes = 120,
                    CreatedAt = DateTime.UtcNow.AddMinutes(7)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var beforeRequest = DateTime.UtcNow;
        var response = await client.GetAsync("/Printers");
        var afterRequest = DateTime.UtcNow;
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Only Imported/Planned jobs belong in the queue: 3 jobs, 480 minutes = 8h 0min.
        Assert.Contains($"/PrintJobs?printerId={ids.PrinterId}", html);
        Assert.Contains("3 produção(ões)", html);
        Assert.Contains("8h 0min", html);
        Assert.Contains("Fila alta", html);
        Assert.DoesNotContain("printers-queue-completed", html);

        Assert.Contains("title=\"Estimativa assume impressão sequencial e ininterrupta a partir de agora; não considera paradas, manutenção ou impressões simultâneas.\"", html);
        var clearAtMatch = Regex.Match(html, @"Livre em (\d{2}/\d{2} \d{2}:\d{2})");
        Assert.True(clearAtMatch.Success, "Expected the estimated queue clear-time hint to render.");
        var renderedClearAt = DateTime.ParseExact(clearAtMatch.Groups[1].Value, "dd/MM HH:mm", CultureInfo.InvariantCulture);
        var expectedMin = beforeRequest.AddMinutes(480).ToLocalTime().AddMinutes(-1);
        var expectedMax = afterRequest.AddMinutes(480).ToLocalTime().AddMinutes(1);
        // "dd/MM HH:mm" drops the year; compare using expectedMin's year for the day/month/hour/minute fields.
        var renderedClearAtWithYear = new DateTime(expectedMin.Year, renderedClearAt.Month, renderedClearAt.Day, renderedClearAt.Hour, renderedClearAt.Minute, 0);
        Assert.InRange(renderedClearAtWithYear, expectedMin.AddSeconds(-59), expectedMax);
    }

    [Fact]
    public async Task PrintJobsIndex_FiltersByPrinter_WhenPrinterIdProvided()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);
        var secondPrinterId = Guid.NewGuid();
        var linkedImportId = Guid.NewGuid();
        var printerAJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Printers.Add(new Printer
            {
                Id = secondPrinterId,
                Name = "Printer B",
                Brand = "E2E",
                Model = "Model B",
                CreatedAt = DateTime.UtcNow
            });

            context.PrintImports.Add(new PrintImport
            {
                Id = linkedImportId,
                FileName = "printer-a-job.gcode",
                FileType = "gcode",
                Status = PrintImportStatus.Parsed,
                ImportedAt = DateTime.UtcNow
            });

            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = printerAJobId,
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    PrintImportId = linkedImportId,
                    SourceFileName = "printer-a-job.gcode",
                    Status = PrintJobStatus.Imported,
                    EstimatedTimeMinutes = 60,
                    CreatedAt = DateTime.UtcNow.AddMinutes(10)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = secondPrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "printer-b-job.gcode",
                    Status = PrintJobStatus.Imported,
                    EstimatedTimeMinutes = 45,
                    CreatedAt = DateTime.UtcNow.AddMinutes(9)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var filteredResponse = await client.GetAsync($"/PrintJobs?printerId={ids.PrinterId}");
        var filteredHtml = await filteredResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.Contains("printer-a-job.gcode", filteredHtml);
        Assert.DoesNotContain("printer-b-job.gcode", filteredHtml);
        Assert.Contains("Filtrando por impressora:", filteredHtml);
        Assert.Contains("Printer A", filteredHtml);
        Assert.Contains("Ver todas", filteredHtml);
        Assert.Contains($"/PrintImports/Details/{linkedImportId}", filteredHtml);

        // Row action links from a printer-filtered queue must carry filterPrinterId so Details/Edit/Delete can round-trip back to it.
        Assert.Contains($"/PrintJobs/Details/{printerAJobId}?filterPrinterId={ids.PrinterId}", filteredHtml);
        Assert.Contains($"/PrintJobs/Edit/{printerAJobId}?filterPrinterId={ids.PrinterId}", filteredHtml);
        Assert.Contains($"/PrintJobs/Delete/{printerAJobId}?filterPrinterId={ids.PrinterId}", filteredHtml);

        var unfilteredResponse = await client.GetAsync("/PrintJobs");
        var unfilteredHtml = await unfilteredResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, unfilteredResponse.StatusCode);
        Assert.Contains("printer-a-job.gcode", unfilteredHtml);
        Assert.Contains("printer-b-job.gcode", unfilteredHtml);
        Assert.DoesNotContain("Filtrando por impressora:", unfilteredHtml);
        // printer-a-job.gcode has a linked import and should render as a link; printer-b-job.gcode has none and stays plain text.
        Assert.Contains($"href=\"/PrintImports/Details/{linkedImportId}\">printer-a-job.gcode</a>", unfilteredHtml);
        Assert.DoesNotContain("href=\"/PrintImports/Details/\">printer-b-job.gcode", unfilteredHtml);
        // Unfiltered rows must not carry a printerId, since there is no filtered queue to return to.
        Assert.DoesNotContain("printerId=", unfilteredHtml);
    }

    [Fact]
    public async Task PrintJobsDetails_PreservesPrinterFilterContext_ThroughEditAndDelete()
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
                SourceFileName = "printer-filter-returnto.gcode",
                Status = PrintJobStatus.Imported,
                EstimatedTimeMinutes = 30,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var indexHtml = await client.GetStringAsync($"/PrintJobs?printerId={ids.PrinterId}");
        Assert.Contains($"/PrintJobs/Details/{printJobId}?filterPrinterId={ids.PrinterId}", indexHtml);
        Assert.Contains($"/PrintJobs/Edit/{printJobId}?filterPrinterId={ids.PrinterId}", indexHtml);
        Assert.Contains($"/PrintJobs/Delete/{printJobId}?filterPrinterId={ids.PrinterId}", indexHtml);

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}?filterPrinterId={ids.PrinterId}");
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains($"href=\"/PrintJobs?printerId={ids.PrinterId}\">Voltar para produções desta impressora</a>", detailsHtml);
        Assert.Contains($"/PrintJobs/Edit/{printJobId}?filterPrinterId={ids.PrinterId}", detailsHtml);
        Assert.Contains($"/PrintJobs/Delete/{printJobId}?filterPrinterId={ids.PrinterId}", detailsHtml);

        var editHtml = WebUtility.HtmlDecode(await client.GetStringAsync($"/PrintJobs/Edit/{printJobId}?filterPrinterId={ids.PrinterId}"));
        Assert.Contains($"name=\"filterPrinterId\" value=\"{ids.PrinterId}\"", editHtml);

        var editToken = ExtractAntiForgeryToken(editHtml);
        var editPostResponse = await client.PostAsync(
            $"/PrintJobs/Edit/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = editToken,
                ["Id"] = printJobId.ToString(),
                ["ProductId"] = ids.ProductId.ToString(),
                ["PrinterId"] = ids.PrinterId.ToString(),
                ["MaterialId"] = ids.MaterialId.ToString(),
                ["EstimatedTimeMinutes"] = "30",
                ["Status"] = PrintJobStatus.Imported,
                ["filterPrinterId"] = ids.PrinterId.ToString()
            }));

        Assert.Equal(HttpStatusCode.Redirect, editPostResponse.StatusCode);
        Assert.Equal(
            $"/PrintJobs/Details/{printJobId}?filterPrinterId={ids.PrinterId}",
            editPostResponse.Headers.Location?.ToString());

        var deleteHtml = WebUtility.HtmlDecode(await client.GetStringAsync($"/PrintJobs/Delete/{printJobId}?filterPrinterId={ids.PrinterId}"));
        Assert.Contains($"name=\"filterPrinterId\" value=\"{ids.PrinterId}\"", deleteHtml);

        var deleteToken = ExtractAntiForgeryToken(deleteHtml);
        var deletePostResponse = await client.PostAsync(
            $"/PrintJobs/Delete/{printJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = deleteToken,
                ["Id"] = printJobId.ToString(),
                ["filterPrinterId"] = ids.PrinterId.ToString()
            }));

        // Deleting from a printer-filtered queue must return to that filtered queue, not to the unfiltered Index.
        Assert.Equal(HttpStatusCode.Redirect, deletePostResponse.StatusCode);
        Assert.Equal($"/PrintJobs?printerId={ids.PrinterId}", deletePostResponse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task PrintJobsIndex_FiltersFailedProductions_WhenStatusFailedRequested()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var failedJobId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.PrintJobs.AddRange(
                new PrintJob
                {
                    Id = failedJobId,
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "status-filter-failed.gcode",
                    Status = PrintJobStatus.Failed,
                    FailureReason = "Entupimento do bico.",
                    CreatedAt = DateTime.UtcNow.AddMinutes(10)
                },
                new PrintJob
                {
                    Id = Guid.NewGuid(),
                    ProductId = ids.ProductId,
                    PrinterId = ids.PrinterId,
                    MaterialId = ids.MaterialId,
                    SourceFileName = "status-filter-imported.gcode",
                    Status = PrintJobStatus.Imported,
                    CreatedAt = DateTime.UtcNow.AddMinutes(9)
                });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var filteredResponse = await client.GetAsync("/PrintJobs?status=failed");
        var filteredHtml = await filteredResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, filteredResponse.StatusCode);
        Assert.Contains("status-filter-failed.gcode", filteredHtml);
        Assert.DoesNotContain("status-filter-imported.gcode", filteredHtml);
        Assert.Contains("Falhas (1)", filteredHtml);
        // Row action links from the filtered queue must carry returnTo=failedQueue so Details/Edit/Delete can round-trip back to it.
        Assert.Contains($"/PrintJobs/Details/{failedJobId}?returnTo=failedQueue", filteredHtml);
        Assert.Contains($"/PrintJobs/Edit/{failedJobId}?returnTo=failedQueue", filteredHtml);
        Assert.Contains($"/PrintJobs/Delete/{failedJobId}?returnTo=failedQueue", filteredHtml);

        var unfilteredResponse = await client.GetAsync("/PrintJobs");
        var unfilteredHtml = await unfilteredResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, unfilteredResponse.StatusCode);
        Assert.Contains("status-filter-failed.gcode", unfilteredHtml);
        Assert.Contains("status-filter-imported.gcode", unfilteredHtml);
        Assert.Contains("Falhas (1)", unfilteredHtml);
        // Unfiltered rows must not carry a returnTo, since there is no filtered queue to return to.
        Assert.Contains($"/PrintJobs/Details/{failedJobId}\"", unfilteredHtml);

        var dashboardResponse = await client.GetAsync("/Dashboard");
        var dashboardHtml = await dashboardResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Contains("href=\"/PrintJobs?status=failed\">Ver falhas</a>", dashboardHtml);

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{failedJobId}?returnTo=failedQueue");
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("href=\"/PrintJobs?status=failed\">Voltar para falhas</a>", detailsHtml);

        var deleteResponse = await client.GetAsync($"/PrintJobs/Delete/{failedJobId}?returnTo=failedQueue");
        var deleteHtml = WebUtility.HtmlDecode(await deleteResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Contains("name=\"returnTo\" value=\"failedQueue\"", deleteHtml);

        var deleteToken = ExtractAntiForgeryToken(deleteHtml);
        var deletePostResponse = await client.PostAsync(
            $"/PrintJobs/Delete/{failedJobId}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = deleteToken,
                ["Id"] = failedJobId.ToString(),
                ["returnTo"] = "failedQueue"
            }));

        // Deleting from the failed queue must return to the filtered failed queue, not to Index or any linked import.
        Assert.Equal(HttpStatusCode.Redirect, deletePostResponse.StatusCode);
        Assert.Equal("/PrintJobs?status=failed", deletePostResponse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task PrintJobPackagingCost_PersistsAndRendersAcrossProductionFlow()
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
                PackagingCost = 1.75m,
                Status = PrintJobStatus.Imported,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();
        var detailsHtml = WebUtility.HtmlDecode(await client.GetStringAsync($"/PrintJobs/Details/{printJobId}"));
        var indexHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/PrintJobs"));
        var editResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        var editHtml = WebUtility.HtmlDecode(await editResponse.Content.ReadAsStringAsync());

        Assert.Contains("Custo de embalagem", detailsHtml);
        Assert.Contains("1,75", detailsHtml);
        Assert.Contains("<th>Custo de embalagem</th>", indexHtml);
        Assert.Contains("name=\"PackagingCost\"", editHtml);

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
                ["PackagingCost"] = "2.25",
                ["Status"] = PrintJobStatus.Imported
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2.25m, (await context.PrintJobs.SingleAsync(job => job.Id == printJobId)).PackagingCost);
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

    [Fact]
    public async Task EditPrintJob_PersistsAndRendersUnitsProduced_OnDetailsAndIndex()
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
                SourceFileName = "units-produced.gcode",
                Status = PrintJobStatus.Planned,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/PrintJobs/Edit/{printJobId}");
        var getHtml = WebUtility.HtmlDecode(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Contains("Unidades produzidas", getHtml);

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
                ["SourceFileName"] = "units-produced.gcode",
                ["CreatedAt"] = "2026-06-19T02:00:00Z",
                ["FilamentUsedGrams"] = "5.00",
                ["Status"] = PrintJobStatus.Completed,
                ["UnitsProduced"] = "4"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.Id == printJobId);
        Assert.Equal(4, printJob.UnitsProduced);

        var detailsResponse = await client.GetAsync($"/PrintJobs/Details/{printJobId}");
        var detailsHtml = WebUtility.HtmlDecode(await detailsResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.Contains("Unidades produzidas", detailsHtml);
        Assert.Contains("<dd class=\"col-sm-9\">4</dd>", detailsHtml);

        var indexResponse = await client.GetAsync("/PrintJobs");
        var indexHtml = WebUtility.HtmlDecode(await indexResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        Assert.Contains("Unidades", indexHtml);
        Assert.Contains("<td>4</td>", indexHtml);
    }

    [Fact]
    public async Task CreatePrintJob_FromImport_PersistsUnitsProduced_DefaultingToOne()
    {
        using var factory = new ThreeDManagerWebFactory();
        var ids = await SeedCommonAsync(factory);

        var importId = Guid.NewGuid();
        await factory.SeedAsync(async context =>
        {
            context.PrintImports.Add(new PrintImport
            {
                Id = importId,
                FileName = "units-produced-import.gcode",
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
        var getHtml = WebUtility.HtmlDecode(await getResponse.Content.ReadAsStringAsync());
        Assert.Contains("Unidades produzidas", getHtml);

        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["ImportId"] = importId.ToString(),
            ["FileName"] = "units-produced-import.gcode",
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

        using var verifyScope = factory.Services.CreateScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var printJob = await context.PrintJobs.SingleAsync(printJob => printJob.PrintImportId == importId);
        Assert.Equal(1, printJob.UnitsProduced);
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

    private static int ExtractDashboardCardCount(string html, string label)
    {
        var match = Regex.Match(
            html,
            $"{Regex.Escape(label)}</h6>\\s*<h2>(\\d+)</h2>");
        Assert.True(match.Success, $"Could not find dashboard card count for '{label}'.");
        return int.Parse(match.Groups[1].Value);
    }
}
