using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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
    public DbSet<Review> Reviews { get; set; }

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

        // DB sequence instead of app-side MAX+1 so concurrent checkouts can't collide on a number.
        modelBuilder.HasSequence<int>("order_numbers").StartsAt(1);

        modelBuilder.Entity<Order>()
            .Property(o => o.OrderNumber)
            .HasDefaultValueSql("nextval('order_numbers')")
            .ValueGeneratedOnAdd();

        modelBuilder.Entity<Order>()
            .HasIndex(o => o.OrderNumber)
            .IsUnique();

        // One review per customer per business — resubmitting updates the existing row.
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.BusinessId, r.UserId })
            .IsUnique();

        // Optimistic concurrency so two managers confirming the same package can't oversell stock.
        modelBuilder.Entity<Package>()
            .Property<uint>("xmin")
            .IsRowVersion();

        // Npgsql rejects Kind=Unspecified for timestamptz columns; tag as UTC rather than convert,
        // since the app has no timezone handling of its own.
        var utcDateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(DateTime)))
        {
            property.SetValueConverter(utcDateTimeConverter);
        }
    }
}