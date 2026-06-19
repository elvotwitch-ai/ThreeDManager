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
                Status = "Parsed",
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
                Status = "Parsed",
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
