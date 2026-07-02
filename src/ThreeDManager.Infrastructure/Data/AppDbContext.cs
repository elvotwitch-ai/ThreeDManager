using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;

namespace ThreeDManager.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialStockMovement> MaterialStockMovements => Set<MaterialStockMovement>();
    public DbSet<ProductStockMovement> ProductStockMovements => Set<ProductStockMovement>();
    public DbSet<PrintImport> PrintImports => Set<PrintImport>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Printer>().ToTable("printers");
        modelBuilder.Entity<Material>().ToTable("materials");
        modelBuilder.Entity<MaterialStockMovement>().ToTable("material_stock_movements");
        modelBuilder.Entity<ProductStockMovement>().ToTable("product_stock_movements");
        modelBuilder.Entity<PrintImport>().ToTable("print_imports");
        modelBuilder.Entity<PrintJob>().ToTable("print_jobs");
    }
}
