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

        var restaurantTypeId = new Guid("11111111-0000-0000-0000-000000000001");
        var bakeryTypeId     = new Guid("11111111-0000-0000-0000-000000000002");
        var cafeTypeId       = new Guid("11111111-0000-0000-0000-000000000003");

        modelBuilder.Entity<BusinessType>().HasData(
            new BusinessType { Id = restaurantTypeId, Name = "Restaurant" },
            new BusinessType { Id = bakeryTypeId,     Name = "Bakery" },
            new BusinessType { Id = cafeTypeId,       Name = "Cafe" }
        );

        var surpriseBagTypeId = new Guid("22222222-0000-0000-0000-000000000001");
        var mealBoxTypeId     = new Guid("22222222-0000-0000-0000-000000000002");
        var breadBagTypeId    = new Guid("22222222-0000-0000-0000-000000000003");

        modelBuilder.Entity<PackageType>().HasData(
            new PackageType { Id = surpriseBagTypeId, Name = "Surprise Bag" },
            new PackageType { Id = mealBoxTypeId,     Name = "Meal Box" },
            new PackageType { Id = breadBagTypeId,    Name = "Bread Bag" }
        );

        var pendingStatusId   = new Guid("33333333-0000-0000-0000-000000000001");
        var confirmedStatusId = new Guid("33333333-0000-0000-0000-000000000002");
        var completedStatusId = new Guid("33333333-0000-0000-0000-000000000003");
        var cancelledStatusId = new Guid("33333333-0000-0000-0000-000000000004");

        modelBuilder.Entity<Status>().HasData(
            new Status { Id = pendingStatusId,   Name = "Pending" },
            new Status { Id = confirmedStatusId, Name = "Confirmed" },
            new Status { Id = completedStatusId, Name = "Completed" },
            new Status { Id = cancelledStatusId, Name = "Cancelled" }
        );

        var business1Id = new Guid("44444444-0000-0000-0000-000000000001");
        var business2Id = new Guid("44444444-0000-0000-0000-000000000002");
        var business3Id = new Guid("44444444-0000-0000-0000-000000000003");

        modelBuilder.Entity<Business>().HasData(
            new Business
            {
                Id             = business1Id,
                Name           = "Green Fork",
                Description    = "Farm-to-table restaurant with daily surplus meals.",
                Address        = "12 Oak Street, Cluj-Napoca",
                BusinessTypeId = restaurantTypeId
            },
            new Business
            {
                Id             = business2Id,
                Name           = "Sunrise Bakery",
                Description    = "Artisan bakery with fresh bread and pastries every morning.",
                Address        = "5 Bread Lane, Cluj-Napoca",
                BusinessTypeId = bakeryTypeId
            },
            new Business
            {
                Id             = business3Id,
                Name           = "The Daily Grind",
                Description    = "Specialty coffee shop with homemade snacks.",
                Address        = "88 Central Boulevard, Cluj-Napoca",
                BusinessTypeId = cafeTypeId
            }
        );

        var pickupStart = new DateTime(2026, 7, 4, 17, 0, 0, DateTimeKind.Utc);
        var pickupEnd   = new DateTime(2026, 7, 4, 20, 0, 0, DateTimeKind.Utc);

        modelBuilder.Entity<Package>().HasData(
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000001"),
                BusinessId    = business1Id,
                PackageTypeId = surpriseBagTypeId,
                Name          = "Green Fork Surprise Bag",
                Description   = "A surprise selection of today's leftover dishes — always delicious.",
                Price         = 12.99m,
                Quantity      = 5,
                PickupStart   = pickupStart,
                PickupEnd     = pickupEnd
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000002"),
                BusinessId    = business1Id,
                PackageTypeId = mealBoxTypeId,
                Name          = "Green Fork Meal Box",
                Description   = "A full meal box with a main course and side dish.",
                Price         = 9.99m,
                Quantity      = 3,
                PickupStart   = pickupStart,
                PickupEnd     = pickupEnd
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000003"),
                BusinessId    = business2Id,
                PackageTypeId = breadBagTypeId,
                Name          = "Sunrise Bread Bag",
                Description   = "Assorted fresh breads and pastries from the day.",
                Price         = 6.50m,
                Quantity      = 10,
                PickupStart   = new DateTime(2026, 7, 4, 16, 0, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 19, 0, 0, DateTimeKind.Utc)
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000004"),
                BusinessId    = business3Id,
                PackageTypeId = surpriseBagTypeId,
                Name          = "Daily Grind Surprise Bag",
                Description   = "Leftover sandwiches, muffins, and snacks from the cafe.",
                Price         = 7.99m,
                Quantity      = 4,
                PickupStart   = new DateTime(2026, 7, 4, 18, 0, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 21, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}