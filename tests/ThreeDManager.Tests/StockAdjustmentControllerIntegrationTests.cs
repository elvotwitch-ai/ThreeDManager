using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;

namespace ThreeDManager.Tests;

// Verifies two behaviors of the manual AdjustStock flows:
// (1) the no-op guard — a "Definir" (Set) adjustment to the value the item already holds must
//     not persist a misleading zero-quantity ledger entry, mirroring the Edit flow's guard;
// (2) the mode-conditional quantity validation — "Definir" (Set) accepts 0 as a valid target
//     (e.g. "o material acabou, corrigir para 0"), while Add/Remove still require a strictly
//     positive quantity. Enforced per-mode via IValidatableObject on the view-models.
public class StockAdjustmentControllerIntegrationTests
{
    [Fact]
    public async Task Materials_AdjustStock_SetToCurrentValue_RecordsNoMovementAndLeavesStockUnchanged()
    {
        using var factory = new ThreeDManagerWebFactory();
        var materialId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Materials.Add(new Material
            {
                Id = materialId,
                Name = "PLA Preto",
                Type = "PLA",
                CurrentStockGrams = 500m,
                MinimumStockGrams = 100m,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Materials/AdjustStock/{materialId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/Materials/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["MaterialId"] = materialId.ToString(),
                ["AdjustmentType"] = "Set",
                ["QuantityGrams"] = "500"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/Materials/Details/{materialId}", postResponse.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(m => m.Id == materialId);
        var movementCount = await context.MaterialStockMovements.CountAsync(m => m.MaterialId == materialId);

        Assert.Equal(500m, material.CurrentStockGrams);
        Assert.Equal(0, movementCount);
    }

    [Fact]
    public async Task Materials_AdjustStock_SetToDifferentValue_RecordsMovementAndUpdatesStock()
    {
        using var factory = new ThreeDManagerWebFactory();
        var materialId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Materials.Add(new Material
            {
                Id = materialId,
                Name = "PLA Preto",
                Type = "PLA",
                CurrentStockGrams = 500m,
                MinimumStockGrams = 100m,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Materials/AdjustStock/{materialId}");
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/Materials/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["MaterialId"] = materialId.ToString(),
                ["AdjustmentType"] = "Set",
                ["QuantityGrams"] = "650"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(m => m.Id == materialId);
        var movement = await context.MaterialStockMovements.SingleAsync(m => m.MaterialId == materialId);

        Assert.Equal(650m, material.CurrentStockGrams);
        Assert.Equal(150m, movement.QuantityGrams);
        Assert.Equal(500m, movement.StockBeforeGrams);
        Assert.Equal(650m, movement.StockAfterGrams);
    }

    [Fact]
    public async Task Products_AdjustStock_SetToCurrentValue_RecordsNoMovementAndLeavesStockUnchanged()
    {
        using var factory = new ThreeDManagerWebFactory();
        var productId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Products.Add(new Product
            {
                Id = productId,
                Name = "Suporte de fone",
                StockQuantity = 12,
                MinimumStockQuantity = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Products/AdjustStock/{productId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/Products/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ProductId"] = productId.ToString(),
                ["AdjustmentType"] = "Set",
                ["QuantityUnits"] = "12"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);
        Assert.Equal($"/Products/Details/{productId}", postResponse.Headers.Location?.ToString());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(p => p.Id == productId);
        var movementCount = await context.ProductStockMovements.CountAsync(m => m.ProductId == productId);

        Assert.Equal(12, product.StockQuantity);
        Assert.Equal(0, movementCount);
    }

    [Fact]
    public async Task Products_AdjustStock_SetToDifferentValue_RecordsMovementAndUpdatesStock()
    {
        using var factory = new ThreeDManagerWebFactory();
        var productId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Products.Add(new Product
            {
                Id = productId,
                Name = "Suporte de fone",
                StockQuantity = 12,
                MinimumStockQuantity = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Products/AdjustStock/{productId}");
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/Products/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ProductId"] = productId.ToString(),
                ["AdjustmentType"] = "Set",
                ["QuantityUnits"] = "20"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(p => p.Id == productId);
        var movement = await context.ProductStockMovements.SingleAsync(m => m.ProductId == productId);

        Assert.Equal(20, product.StockQuantity);
        Assert.Equal(8, movement.QuantityUnits);
        Assert.Equal(12, movement.StockBeforeUnits);
        Assert.Equal(20, movement.StockAfterUnits);
    }

    [Fact]
    public async Task Materials_AdjustStock_SetToZero_RecordsMovementAndZerosStock()
    {
        using var factory = new ThreeDManagerWebFactory();
        var materialId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Materials.Add(new Material
            {
                Id = materialId,
                Name = "PLA Preto",
                Type = "PLA",
                CurrentStockGrams = 500m,
                MinimumStockGrams = 100m,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Materials/AdjustStock/{materialId}");
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/Materials/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["MaterialId"] = materialId.ToString(),
                ["AdjustmentType"] = "Set",
                ["QuantityGrams"] = "0"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(m => m.Id == materialId);
        var movement = await context.MaterialStockMovements.SingleAsync(m => m.MaterialId == materialId);

        Assert.Equal(0m, material.CurrentStockGrams);
        Assert.Equal(-500m, movement.QuantityGrams);
        Assert.Equal(500m, movement.StockBeforeGrams);
        Assert.Equal(0m, movement.StockAfterGrams);
    }

    [Fact]
    public async Task Materials_AdjustStock_AddZero_IsRejectedAndLeavesStockUnchanged()
    {
        using var factory = new ThreeDManagerWebFactory();
        var materialId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Materials.Add(new Material
            {
                Id = materialId,
                Name = "PLA Preto",
                Type = "PLA",
                CurrentStockGrams = 500m,
                MinimumStockGrams = 100m,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Materials/AdjustStock/{materialId}");
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/Materials/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["MaterialId"] = materialId.ToString(),
                ["AdjustmentType"] = "Add",
                ["QuantityGrams"] = "0"
            }));

        // Invalid model state re-renders the form (200), not a redirect.
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var material = await context.Materials.SingleAsync(m => m.Id == materialId);
        var movementCount = await context.MaterialStockMovements.CountAsync(m => m.MaterialId == materialId);

        Assert.Equal(500m, material.CurrentStockGrams);
        Assert.Equal(0, movementCount);
    }

    [Fact]
    public async Task Products_AdjustStock_SetToZero_RecordsMovementAndZerosStock()
    {
        using var factory = new ThreeDManagerWebFactory();
        var productId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Products.Add(new Product
            {
                Id = productId,
                Name = "Suporte de fone",
                StockQuantity = 12,
                MinimumStockQuantity = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Products/AdjustStock/{productId}");
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/Products/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ProductId"] = productId.ToString(),
                ["AdjustmentType"] = "Set",
                ["QuantityUnits"] = "0"
            }));

        Assert.Equal(HttpStatusCode.Redirect, postResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(p => p.Id == productId);
        var movement = await context.ProductStockMovements.SingleAsync(m => m.ProductId == productId);

        Assert.Equal(0, product.StockQuantity);
        Assert.Equal(-12, movement.QuantityUnits);
        Assert.Equal(12, movement.StockBeforeUnits);
        Assert.Equal(0, movement.StockAfterUnits);
    }

    [Fact]
    public async Task Products_AdjustStock_RemoveZero_IsRejectedAndLeavesStockUnchanged()
    {
        using var factory = new ThreeDManagerWebFactory();
        var productId = Guid.NewGuid();

        await factory.SeedAsync(async context =>
        {
            context.Products.Add(new Product
            {
                Id = productId,
                Name = "Suporte de fone",
                StockQuantity = 12,
                MinimumStockQuantity = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        });

        using var client = factory.CreateTestClient();

        var getResponse = await client.GetAsync($"/Products/AdjustStock/{productId}");
        var token = ExtractAntiForgeryToken(await getResponse.Content.ReadAsStringAsync());

        var postResponse = await client.PostAsync(
            "/Products/AdjustStock",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
                ["ProductId"] = productId.ToString(),
                ["AdjustmentType"] = "Remove",
                ["QuantityUnits"] = "0"
            }));

        // Invalid model state re-renders the form (200), not a redirect.
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await context.Products.SingleAsync(p => p.Id == productId);
        var movementCount = await context.ProductStockMovements.CountAsync(m => m.ProductId == productId);

        Assert.Equal(12, product.StockQuantity);
        Assert.Equal(0, movementCount);
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "Could not find antiforgery token in form html.");
        return match.Groups[1].Value;
    }
}
