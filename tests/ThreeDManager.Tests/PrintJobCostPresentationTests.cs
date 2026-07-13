using ThreeDManager.Web.Presentation;

namespace ThreeDManager.Tests;

public class PrintJobCostPresentationTests
{
    [Fact]
    public void EstimatedCostPerUnit_DividesTotalAcrossUnits()
    {
        Assert.Equal(3.50m, PrintJobCostPresentation.EstimatedCostPerUnit(14.00m, 4));
    }

    [Fact]
    public void EstimatedCostPerUnit_ForSingleUnit_EqualsTotal()
    {
        Assert.Equal(14.00m, PrintJobCostPresentation.EstimatedCostPerUnit(14.00m, 1));
    }

    [Theory]
    [InlineData(null, 4)]
    [InlineData(14.00, 0)]
    public void EstimatedCostPerUnit_ReturnsNull_WhenTotalMissingOrUnitsNotPositive(double? total, int units)
    {
        Assert.Null(PrintJobCostPresentation.EstimatedCostPerUnit((decimal?)total, units));
    }

    [Fact]
    public void EstimatedMargin_ComparesSalePriceAgainstPerUnitCost()
    {
        // Per-unit margin for a batch job: R$ 20,00 sale price - R$ 3,50 per-unit cost = R$ 16,50.
        Assert.Equal(16.50m, PrintJobCostPresentation.EstimatedMargin(20.00m, 3.50m));
    }

    [Fact]
    public void EstimatedMargin_ReturnsNull_WhenSalePriceOrCostMissing()
    {
        Assert.Null(PrintJobCostPresentation.EstimatedMargin(null, 3.50m));
        Assert.Null(PrintJobCostPresentation.EstimatedMargin(20.00m, null));
    }

    [Fact]
    public void EstimatedMarginPercentage_IsMarginOverSalePrice()
    {
        Assert.Equal(82.5m, PrintJobCostPresentation.EstimatedMarginPercentage(16.50m, 20.00m));
    }

    [Fact]
    public void EstimatedMarginPercentage_ReturnsNull_WhenSalePriceNotPositive()
    {
        Assert.Null(PrintJobCostPresentation.EstimatedMarginPercentage(16.50m, 0m));
    }

    [Fact]
    public void SuggestedSalePrice_AppliesTargetMarginToPerUnitCost()
    {
        // R$ 3,50 per-unit cost + 50% target margin = R$ 5,25.
        Assert.Equal(5.25m, PrintJobCostPresentation.SuggestedSalePrice(3.50m, 50.0m));
    }

    [Fact]
    public void SuggestedSalePrice_ReturnsNull_WhenCostOrTargetMarginMissing()
    {
        Assert.Null(PrintJobCostPresentation.SuggestedSalePrice(null, 50.0m));
        Assert.Null(PrintJobCostPresentation.SuggestedSalePrice(3.50m, null));
    }
}
