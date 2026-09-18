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
    public DbSet<OrderPickupPass> OrderPickupPasses { get; set; }
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
    public DbSet<BusinessHours> BusinessHours { get; set; }
    public DbSet<BusinessClosure> BusinessClosures { get; set; }
    public DbSet<PushSubscription> PushSubscriptions { get; set; }
    public DbSet<RescueCircle> RescueCircles { get; set; }
    public DbSet<RescueCircleParticipant> RescueCircleParticipants { get; set; }
    public DbSet<StandingOrder> StandingOrders { get; set; }
    public DbSet<KitchenTip> KitchenTips { get; set; }
    public DbSet<Referral> Referrals { get; set; }
    public DbSet<StoreCreditEntry> StoreCreditEntries { get; set; }
    public DbSet<Brand> Brands { get; set; }
    public DbSet<BrandFavorite> BrandFavorites { get; set; }

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

        // A pass is always looked up by (OrderId, its own Id) from the pickup/validate routes —
        // deleting the order deletes its passes along with it.
        modelBuilder.Entity<OrderPickupPass>()
            .HasOne(p => p.Order)
            .WithMany(o => o.PickupPasses)
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderPickupPass>()
            .HasIndex(p => p.OrderId);

        // One review per customer per business — resubmitting updates the existing row.
        modelBuilder.Entity<Review>()
            .HasIndex(r => new { r.BusinessId, r.UserId })
            .IsUnique();

        // Deleting a package un-tags any review that pointed at it rather than deleting the review.
        modelBuilder.Entity<Review>()
            .HasOne(r => r.Package)
            .WithMany()
            .HasForeignKey(r => r.PackageId)
            .OnDelete(DeleteBehavior.SetNull);

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

        // One row per weekday per business — SetHoursAsync always replaces the full set together.
        modelBuilder.Entity<BusinessHours>()
            .HasIndex(h => new { h.BusinessId, h.DayOfWeek })
            .IsUnique();

        modelBuilder.Entity<BusinessHours>()
            .HasOne(h => h.Business)
            .WithMany(b => b.Hours)
            .HasForeignKey(h => h.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BusinessClosure>()
            .HasOne(c => c.Business)
            .WithMany(b => b.Closures)
            .HasForeignKey(c => c.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        // A resubscribing browser gets a fresh row rather than a duplicate — see PushSubscription.
        modelBuilder.Entity<PushSubscription>()
            .HasIndex(s => s.Endpoint)
            .IsUnique();

        modelBuilder.Entity<PushSubscription>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One Rescue Circle per Order — deleting the order (it never happens outside tests, but
        // mirrors OrderPickupPass's own cascade) takes the circle and its participants with it.
        modelBuilder.Entity<RescueCircle>()
            .HasOne(c => c.Order)
            .WithOne(o => o.RescueCircle)
            .HasForeignKey<RescueCircle>(c => c.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RescueCircle>()
            .HasIndex(c => c.OrderId)
            .IsUnique();

        modelBuilder.Entity<RescueCircle>()
            .HasOne(c => c.Organizer)
            .WithMany()
            .HasForeignKey(c => c.OrganizerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RescueCircleParticipant>()
            .HasOne(p => p.RescueCircle)
            .WithMany(c => c.Participants)
            .HasForeignKey(p => p.RescueCircleId)
            .OnDelete(DeleteBehavior.Cascade);

        // One participant row per (circle, user) — joining twice just re-shows the existing slot.
        modelBuilder.Entity<RescueCircleParticipant>()
            .HasIndex(p => new { p.RescueCircleId, p.UserId })
            .IsUnique();

        modelBuilder.Entity<RescueCircleParticipant>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Disappears with the account or the kitchen it targets; a deleted package-type narrowing
        // just widens it back to "any type" instead of deleting the row.
        modelBuilder.Entity<StandingOrder>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StandingOrder>()
            .HasOne(s => s.Business)
            .WithMany()
            .HasForeignKey(s => s.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StandingOrder>()
            .HasOne(s => s.PackageType)
            .WithMany()
            .HasForeignKey(s => s.PackageTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        // A customer's own standing-order list is the only read pattern.
        modelBuilder.Entity<StandingOrder>()
            .HasIndex(s => s.UserId);

        // A business's own tips feed, newest first, is the only read pattern.
        modelBuilder.Entity<KitchenTip>()
            .HasIndex(t => new { t.BusinessId, t.CreatedAt });

        modelBuilder.Entity<KitchenTip>()
            .HasOne(t => t.Business)
            .WithMany()
            .HasForeignKey(t => t.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<KitchenTip>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A referred user can only ever have been invited once — the first registration is the
        // only one that can still be pending a reward.
        modelBuilder.Entity<Referral>()
            .HasIndex(r => r.ReferredUserId)
            .IsUnique();

        modelBuilder.Entity<Referral>()
            .HasOne(r => r.Referrer)
            .WithMany()
            .HasForeignKey(r => r.ReferrerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Referral>()
            .HasOne(r => r.Referred)
            .WithMany()
            .HasForeignKey(r => r.ReferredUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A user's own balance/history, newest first, is the only read pattern.
        modelBuilder.Entity<StoreCreditEntry>()
            .HasIndex(e => new { e.UserId, e.CreatedAt });

        modelBuilder.Entity<StoreCreditEntry>()
            .HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Nullable unique — any number of not-yet-generated (null) rows are fine, only an actual
        // generated code must be unique.
        modelBuilder.Entity<ApplicationUser>()
            .HasIndex(u => u.ReferralCode)
            .IsUnique();

        // A brand can be deleted (or never assigned) without taking its locations down —
        // unlike BusinessType's required FK, this one just leaves the business standalone.
        modelBuilder.Entity<Business>()
            .HasOne(b => b.Brand)
            .WithMany(br => br.Businesses)
            .HasForeignKey(b => b.BrandId)
            .OnDelete(DeleteBehavior.SetNull);

        // Nullable unique — same convention as WebhookApiKeyHash's column comment: only an actual
        // generated key needs to resolve back to exactly one business.
        modelBuilder.Entity<Business>()
            .HasIndex(b => b.WebhookApiKeyHash)
            .IsUnique();

        // One favorite per customer per brand — same toggle-not-duplicate rule as Favorite.
        modelBuilder.Entity<BrandFavorite>()
            .HasIndex(f => new { f.UserId, f.BrandId })
            .IsUnique();

        modelBuilder.Entity<BrandFavorite>()
            .HasOne(f => f.Brand)
            .WithMany()
            .HasForeignKey(f => f.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BrandFavorite>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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