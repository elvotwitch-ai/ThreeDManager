using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;
using ThreeDManager.Infrastructure.Data;
using ThreeDManager.Infrastructure.Services;

namespace ThreeDManager.Tests;

public class PrintJobStockServiceTests
{
    [Fact]
    public async Task ApplyForNewPrintJobAsync_DeductsStock_WhenPrintJobIsCompleted()
    {
        await using var context = CreateContext();
        var material = await SeedMaterialAsync(context, 1000m);
        var printJob = SeedPrintJob(material.Id, 12.45m, "Completed");

        var service = new PrintJobStockService(context);

        var result = await service.ApplyForNewPrintJobAsync(printJob);
        await context.SaveChangesAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(987.55m, material.CurrentStockGrams);
        Assert.Equal(material.Id, printJob.StockDeductedMaterialId);
        Assert.Equal(12.45m, printJob.StockDeductedGrams);
        Assert.NotNull(printJob.StockDeductedAt);

        var movement = await context.MaterialStockMovements.SingleAsync();
        Assert.Equal(material.Id, movement.MaterialId);
        Assert.Equal(printJob.Id, movement.PrintJobId);
        Assert.Equal("PrintJobCompleted", movement.MovementType);
        Assert.Equal(-12.45m, movement.QuantityGrams);
        Assert.Equal(1000m, movement.StockBeforeGrams);
        Assert.Equal(987.55m, movement.StockAfterGrams);
        Assert.Contains("Baixa automática", movement.Notes);
        Assert.Contains("12.45", movement.Notes);
    }

    [Fact]
    public async Task SyncForEditedPrintJobAsync_RestoresStock_WhenStatusLeavesCompleted()
    {
        await using var context = CreateContext();
        var material = await SeedMaterialAsync(context, 1000m);
        var printJob = SeedPrintJob(material.Id, 12.45m, "Completed");
        var service = new PrintJobStockService(context);

        var initialResult = await service.ApplyForNewPrintJobAsync(printJob);
        await context.SaveChangesAsync();

        Assert.True(initialResult.Succeeded);
        Assert.Equal(987.55m, material.CurrentStockGrams);

        var editResult = await service.SyncForEditedPrintJobAsync(printJob, material.Id, 12.45m, "Failed");
        await context.SaveChangesAsync();

        Assert.True(editResult.Succeeded);
        Assert.Equal(1000m, material.CurrentStockGrams);
        Assert.False(printJob.StockDeductedMaterialId.HasValue);
        Assert.False(printJob.StockDeductedGrams.HasValue);
        Assert.False(printJob.StockDeductedAt.HasValue);

        var movements = await context.MaterialStockMovements.OrderBy(movement => movement.CreatedAt).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal("PrintJobCompleted", movements[0].MovementType);
        Assert.Equal(-12.45m, movements[0].QuantityGrams);
        Assert.Equal("PrintJobStockRestored", movements[1].MovementType);
        Assert.Equal(12.45m, movements[1].QuantityGrams);
        Assert.Contains("Devolução automática", movements[1].Notes);
    }

    [Fact]
    public async Task SyncForEditedPrintJobAsync_ReplacesStockDeduction_WhenGramsChange()
    {
        await using var context = CreateContext();
        var material = await SeedMaterialAsync(context, 1000m);
        var printJob = SeedPrintJob(material.Id, 12.45m, "Completed");
        var service = new PrintJobStockService(context);

        var initialResult = await service.ApplyForNewPrintJobAsync(printJob);
        await context.SaveChangesAsync();

        Assert.True(initialResult.Succeeded);
        Assert.Equal(987.55m, material.CurrentStockGrams);

        var editResult = await service.SyncForEditedPrintJobAsync(printJob, material.Id, 20m, "Completed");
        await context.SaveChangesAsync();

        Assert.True(editResult.Succeeded);
        Assert.Equal(980m, material.CurrentStockGrams);
        Assert.Equal(material.Id, printJob.StockDeductedMaterialId);
        Assert.Equal(20m, printJob.StockDeductedGrams);
        Assert.NotNull(printJob.StockDeductedAt);

        var movements = await context.MaterialStockMovements.OrderBy(movement => movement.CreatedAt).ToListAsync();
        Assert.Equal(3, movements.Count);
        Assert.Equal(-12.45m, movements[0].QuantityGrams);
        Assert.Equal(12.45m, movements[1].QuantityGrams);
        Assert.Equal(-20m, movements[2].QuantityGrams);
        Assert.Contains("Baixa automática", movements[2].Notes);
    }

    [Fact]
    public async Task ApplyForNewPrintJobAsync_Fails_WhenStockIsInsufficient()
    {
        await using var context = CreateContext();
        var material = await SeedMaterialAsync(context, 10m);
        var printJob = SeedPrintJob(material.Id, 12.45m, "Completed");
        var service = new PrintJobStockService(context);

        var result = await service.ApplyForNewPrintJobAsync(printJob);

        Assert.False(result.Succeeded);
        Assert.Equal(nameof(PrintJob.FilamentUsedGrams), result.FieldName);
        Assert.Equal("Estoque insuficiente para concluir esta produção.", result.ErrorMessage);
        Assert.False(printJob.StockDeductedAt.HasValue);
        Assert.False(printJob.StockDeductedMaterialId.HasValue);
        Assert.False(printJob.StockDeductedGrams.HasValue);
        Assert.Empty(await context.MaterialStockMovements.ToListAsync());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Material> SeedMaterialAsync(AppDbContext context, decimal stockGrams)
    {
        var material = new Material
        {
            Id = Guid.NewGuid(),
            Name = "PLA Preto",
            Type = "PLA",
            Brand = "E2E",
            Color = "Black",
            CostPerKg = 80m,
            CurrentStockGrams = stockGrams,
            CreatedAt = DateTime.UtcNow
        };

        context.Materials.Add(material);
        await context.SaveChangesAsync();

        return material;
    }

    private static PrintJob SeedPrintJob(Guid materialId, decimal filamentUsedGrams, string status)
    {
        return new PrintJob
        {
            Id = Guid.NewGuid(),
            MaterialId = materialId,
            FilamentUsedGrams = filamentUsedGrams,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }
}
