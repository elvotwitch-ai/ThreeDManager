using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDmanager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetMarginPercentageToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TargetMarginPercentage",
                table: "products",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetMarginPercentage",
                table: "products");
        }
    }
}
