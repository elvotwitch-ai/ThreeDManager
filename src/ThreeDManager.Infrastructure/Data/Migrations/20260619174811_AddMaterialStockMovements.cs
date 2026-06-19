using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDmanager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "material_stock_movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrintJobId = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementType = table.Column<string>(type: "text", nullable: false),
                    QuantityGrams = table.Column<decimal>(type: "numeric", nullable: false),
                    StockBeforeGrams = table.Column<decimal>(type: "numeric", nullable: true),
                    StockAfterGrams = table.Column<decimal>(type: "numeric", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_material_stock_movements", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "material_stock_movements");
        }
    }
}
