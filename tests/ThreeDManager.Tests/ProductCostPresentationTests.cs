using ThreeDManager.Web.Presentation;

namespace ThreeDManager.Tests;

public class ProductCostPresentationTests
{
    [Fact]
    public void StockValueAtSalePrice_MultipliesSalePriceByUnitsOnHand()
    {
        // R$ 25,00 each; 8 units on hand -> R$ 200,00.
        Assert.Equal(200.00m, ProductCostPresentation.StockValueAtSalePrice(25.00m, 8));
    }

    [Fact]
    public void StockValueAtSalePrice_RoundsToTwoDecimals()
    {
        // R$ 12,345 each; 3 units -> R$ 37,035 -> R$ 37,04.
        Assert.Equal(37.04m, ProductCostPresentation.StockValueAtSalePrice(12.345m, 3));
    }

    [Fact]
    public void StockValueAtSalePrice_IsZero_WhenStockIsZero()
    {
        Assert.Equal(0.00m, ProductCostPresentation.StockValueAtSalePrice(25.00m, 0));
    }

    [Theory]
    [InlineData(null, 8)]
    [InlineData(25.00, null)]
    [InlineData(null, null)]
    public void StockValueAtSalePrice_ReturnsNull_WhenPriceOrStockMissing(double? salePrice, int? stockQuantity)
    {
        Assert.Null(ProductCostPresentation.StockValueAtSalePrice((decimal?)salePrice, stockQuantity));
    }
}
