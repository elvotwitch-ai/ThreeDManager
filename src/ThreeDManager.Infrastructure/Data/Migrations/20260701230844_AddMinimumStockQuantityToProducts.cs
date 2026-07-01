using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDmanager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMinimumStockQuantityToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumStockQuantity",
                table: "products",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumStockQuantity",
                table: "products");
        }
    }
}
