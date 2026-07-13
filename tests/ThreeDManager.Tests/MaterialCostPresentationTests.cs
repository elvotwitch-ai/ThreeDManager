using ThreeDManager.Domain.Entities;
using ThreeDManager.Web.Presentation;

namespace ThreeDManager.Tests;

public class MaterialCostPresentationTests
{
    [Fact]
    public void InventoryValue_MultipliesCostPerGramByGramsOnHand()
    {
        // R$ 120,00/kg -> R$ 0,12/g; 2500 g on hand -> R$ 300,00.
        Assert.Equal(300.00m, MaterialCostPresentation.InventoryValue(120.00m, 2500m));
    }

    [Fact]
    public void InventoryValue_RoundsToTwoDecimals()
    {
        // R$ 89,90/kg -> R$ 0,0899/g; 333 g -> R$ 29,9367 -> R$ 29,94.
        Assert.Equal(29.94m, MaterialCostPresentation.InventoryValue(89.90m, 333m));
    }

    [Fact]
    public void InventoryValue_IsZero_WhenStockIsZero()
    {
        Assert.Equal(0.00m, MaterialCostPresentation.InventoryValue(120.00m, 0m));
    }

    [Theory]
    [InlineData(null, 2500.0)]
    [InlineData(120.00, null)]
    [InlineData(null, null)]
    public void InventoryValue_ReturnsNull_WhenCostOrStockMissing(double? costPerKg, double? grams)
    {
        Assert.Null(MaterialCostPresentation.InventoryValue((decimal?)costPerKg, (decimal?)grams));
    }

    [Fact]
    public void TotalInventoryValue_SumsEachMaterialInventoryValue()
    {
        var materials = new[]
        {
            new Material { CostPerKg = 120.00m, CurrentStockGrams = 2500m }, // R$ 300,00
            new Material { CostPerKg = 100.00m, CurrentStockGrams = 500m },  // R$ 50,00
        };

        Assert.Equal(350.00m, MaterialCostPresentation.TotalInventoryValue(materials));
    }

    [Fact]
    public void TotalInventoryValue_TreatsMaterialsWithMissingDataAsZero()
    {
        var materials = new[]
        {
            new Material { CostPerKg = 120.00m, CurrentStockGrams = 2500m }, // R$ 300,00
            new Material { CostPerKg = null, CurrentStockGrams = 500m },      // unknown cost -> 0
            new Material { CostPerKg = 100.00m, CurrentStockGrams = null },   // unknown stock -> 0
        };

        Assert.Equal(300.00m, MaterialCostPresentation.TotalInventoryValue(materials));
    }

    [Fact]
    public void TotalInventoryValue_IsZero_ForEmptySequence()
    {
        Assert.Equal(0m, MaterialCostPresentation.TotalInventoryValue(Array.Empty<Material>()));
    }
}
