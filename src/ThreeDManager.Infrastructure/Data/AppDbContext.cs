using Microsoft.EntityFrameworkCore;
using ThreeDManager.Domain.Entities;

namespace ThreeDManager.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<Product> products => Set<Product>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<PrintImport> printImports => Set<PrintImport>();
    public DbSet<PrintJob> PrintJobs => Set<PrintJob>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<Printer>().ToTable("printers");
        modelBuilder.Entity<Material>().ToTable("materials");
        modelBuilder.Entity<PrintImport>().ToTable("print_imports");
        modelBuilder.Entity<PrintJob>().ToTable("print_jobs");
    }
}
