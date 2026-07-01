using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDmanager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCostPerHourToPrinters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CostPerHour",
                table: "printers",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CostPerHour",
                table: "printers");
        }
    }
}
