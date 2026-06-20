using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThreeDmanager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameMaterialCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'materials'
          AND column_name = 'CreatedAT'
    ) THEN
        ALTER TABLE materials RENAME COLUMN "CreatedAT" TO "CreatedAt";
    END IF;
END $$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'materials'
          AND column_name = 'CreatedAt'
    ) THEN
        ALTER TABLE materials RENAME COLUMN "CreatedAt" TO "CreatedAT";
    END IF;
END $$;
""");
        }
    }
}
