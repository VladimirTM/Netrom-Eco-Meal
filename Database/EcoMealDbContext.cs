using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Database;

public class EcoMealDbContext : IdentityDbContext<ApplicationUser>
{
    public EcoMealDbContext(DbContextOptions<EcoMealDbContext> options) : base(options)
    {
    }

    public DbSet<Business> Businesses { get; set; }
    public DbSet<BusinessStaff> BusinessStaff { get; set; }
    public DbSet<BusinessType> BusinessTypes { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderPackage> OrderPackages { get; set; }
    public DbSet<Package> Packages { get; set; }
    public DbSet<PackageType> PackageTypes { get; set; }
    public DbSet<Status> Statuses { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<PackageTemplate> PackageTemplates { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PendingCheckout> PendingCheckouts { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<Report> Reports { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicit DB-level default so a NOT NULL backfill on existing rows lands on "Approved"
        // (matching the CLR default) instead of Postgres substituting an empty string.
        modelBuilder.Entity<Business>()
            .Property(b => b.Status)
            .HasDefaultValue(BusinessStatuses.Approved);

        // A business can have several staff, and a staff member can be assigned to several
        // businesses — only the (BusinessId, UserId) pair itself needs to stay unique.
        modelBuilder.Entity<BusinessStaff>()
            .HasIndex(s => new { s.BusinessId, s.UserId })
            .IsUnique();

        modelBuilder.Entity<BusinessStaff>()
            .HasOne(s => s.Business)
            .WithMany(b => b.Staff)
            .HasForeignKey(s => s.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BusinessStaff>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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

        // One favorite per customer per business — toggling un-favorites instead of duplicating.
        modelBuilder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.BusinessId })
            .IsUnique();

        // One Payment per Order — Stripe Checkout is one session per order, never split.
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Order)
            .WithOne(o => o.Payment)
            .HasForeignKey<Payment>(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.OrderId)
            .IsUnique();

        // Newest-first lookups for a user's bell dropdown are the only query pattern.
        modelBuilder.Entity<Notification>()
            .HasIndex(n => new { n.UserId, n.CreatedAt });

        // Newest-first is the only read pattern for the admin audit log table.
        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.CreatedAt);

        // The reports queue only ever filters by status ("Open" by default).
        modelBuilder.Entity<Report>()
            .HasIndex(r => r.Status);

        // Optimistic concurrency so two managers confirming the same package can't oversell stock.
        modelBuilder.Entity<Package>()
            .Property<uint>("xmin")
            .IsRowVersion();

        // Deleting a template stops future generation but leaves already-generated packages intact.
        modelBuilder.Entity<Package>()
            .HasOne(p => p.Template)
            .WithMany(t => t.GeneratedPackages)
            .HasForeignKey(p => p.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

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