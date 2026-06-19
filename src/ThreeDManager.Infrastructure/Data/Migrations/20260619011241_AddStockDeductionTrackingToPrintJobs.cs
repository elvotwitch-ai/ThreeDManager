using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDmanager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockDeductionTrackingToPrintJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StockDeductedAt",
                table: "print_jobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StockDeductedGrams",
                table: "print_jobs",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StockDeductedMaterialId",
                table: "print_jobs",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockDeductedAt",
                table: "print_jobs");

            migrationBuilder.DropColumn(
                name: "StockDeductedGrams",
                table: "print_jobs");

            migrationBuilder.DropColumn(
                name: "StockDeductedMaterialId",
                table: "print_jobs");
        }
    }
}
