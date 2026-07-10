using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Database;

public class EcoMealDbContext : IdentityDbContext<ApplicationUser>
{
    public EcoMealDbContext(DbContextOptions<EcoMealDbContext> options) : base(options)
    {
    }

    public DbSet<Business> Businesses { get; set; }
    public DbSet<BusinessType> BusinessTypes { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderPackage> OrderPackages { get; set; }
    public DbSet<Package> Packages { get; set; }
    public DbSet<PackageType> PackageTypes { get; set; }
    public DbSet<Status> Statuses { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Business>()
            .HasOne(b => b.Manager)
            .WithMany()
            .HasForeignKey(b => b.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Business>()
            .HasIndex(b => b.ManagerId)
            .IsUnique();
    }
}