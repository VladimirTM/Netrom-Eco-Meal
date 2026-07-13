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

        // Order numbers are assigned by a DB sequence (instead of app-side MAX+1) so concurrent
        // checkouts can't race each other into producing the same number.
        modelBuilder.HasSequence<int>("order_numbers").StartsAt(1);

        modelBuilder.Entity<Order>()
            .Property(o => o.OrderNumber)
            .HasDefaultValueSql("nextval('order_numbers')")
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();

        // Optimistic concurrency on Package.Quantity so two managers confirming orders against
        // the same package at the same time can't both succeed and oversell stock.
        modelBuilder.Entity<Package>()
            .Property<uint>("xmin")
            .IsRowVersion();
    }
}