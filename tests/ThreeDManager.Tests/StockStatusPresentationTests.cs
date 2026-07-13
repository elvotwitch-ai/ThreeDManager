using ThreeDManager.Web.Presentation;

namespace ThreeDManager.Tests;

public class StockStatusPresentationTests
{
    [Fact]
    public void IsLowStock_WhenCurrentAtOrBelowMinimum_IsTrue()
    {
        Assert.True(StockStatusPresentation.IsLowStock(200m, 200m)); // at threshold
        Assert.True(StockStatusPresentation.IsLowStock(50m, 200m));  // below threshold
    }

    [Fact]
    public void IsLowStock_WhenCurrentAboveMinimum_IsFalse()
    {
        Assert.False(StockStatusPresentation.IsLowStock(500m, 200m));
    }

    [Theory]
    [InlineData(null, 200.0)]
    [InlineData(200.0, null)]
    [InlineData(null, null)]
    public void IsLowStock_WhenEitherValueMissing_IsFalse(double? current, double? minimum)
    {
        Assert.False(StockStatusPresentation.IsLowStock((decimal?)current, (decimal?)minimum));
    }

    [Fact]
    public void GetLabel_ReturnsLowStock_WhenAtOrBelowMinimum()
    {
        Assert.Equal(StockStatusPresentation.LowStockLabel, StockStatusPresentation.GetLabel(200m, 200m));
        Assert.Equal("Baixo estoque", StockStatusPresentation.GetLabel(50m, 200m));
    }

    [Fact]
    public void GetLabel_ReturnsOk_WhenAboveMinimumAndBothConfigured()
    {
        Assert.Equal(StockStatusPresentation.OkLabel, StockStatusPresentation.GetLabel(500m, 200m));
        Assert.Equal("Ok", StockStatusPresentation.GetLabel(500m, 200m));
    }

    [Theory]
    [InlineData(null, 200.0)]
    [InlineData(500.0, null)]
    [InlineData(null, null)]
    public void GetLabel_ReturnsNoAlert_WhenAlertNotConfigured(double? current, double? minimum)
    {
        Assert.Equal("Sem alerta", StockStatusPresentation.GetLabel((decimal?)current, (decimal?)minimum));
    }

    [Fact]
    public void GetBadgeClass_MatchesTheThreeStates()
    {
        Assert.Equal("badge bg-danger", StockStatusPresentation.GetBadgeClass(50m, 200m));   // low
        Assert.Equal("badge bg-success", StockStatusPresentation.GetBadgeClass(500m, 200m)); // ok
        Assert.Equal("badge bg-secondary", StockStatusPresentation.GetBadgeClass(500m, null)); // no alert
    }
}
