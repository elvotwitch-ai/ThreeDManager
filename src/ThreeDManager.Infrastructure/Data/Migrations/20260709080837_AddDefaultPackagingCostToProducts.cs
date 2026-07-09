using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDmanager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultPackagingCostToProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DefaultPackagingCost",
                table: "products",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultPackagingCost",
                table: "products");
        }
    }
}
