using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDmanager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinishedGoodsStockCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StockCreditedAt",
                table: "print_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockCreditedProductId",
                table: "print_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockCreditedUnits",
                table: "print_jobs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_stock_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementType = table.Column<string>(type: "text", nullable: false),
                    QuantityUnits = table.Column<int>(type: "integer", nullable: false),
                    StockBeforeUnits = table.Column<int>(type: "integer", nullable: true),
                    StockAfterUnits = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_stock_movements", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_stock_movements");

            migrationBuilder.DropColumn(
                name: "StockCreditedAt",
                table: "print_jobs");

            migrationBuilder.DropColumn(
                name: "StockCreditedProductId",
                table: "print_jobs");

            migrationBuilder.DropColumn(
                name: "StockCreditedUnits",
                table: "print_jobs");
        }
    }
}
