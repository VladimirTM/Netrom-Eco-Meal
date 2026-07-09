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

        var restaurantTypeId   = new Guid("11111111-0000-0000-0000-000000000001");
        var bakeryTypeId       = new Guid("11111111-0000-0000-0000-000000000002");
        var cafeTypeId         = new Guid("11111111-0000-0000-0000-000000000003");
        var groceryTypeId      = new Guid("11111111-0000-0000-0000-000000000004");
        var foodTruckTypeId    = new Guid("11111111-0000-0000-0000-000000000005");

        modelBuilder.Entity<BusinessType>().HasData(
            new BusinessType { Id = restaurantTypeId,  Name = "Restaurant" },
            new BusinessType { Id = bakeryTypeId,      Name = "Bakery" },
            new BusinessType { Id = cafeTypeId,        Name = "Cafe" },
            new BusinessType { Id = groceryTypeId,     Name = "Grocery Store" },
            new BusinessType { Id = foodTruckTypeId,   Name = "Food Truck" }
        );

        var surpriseBagTypeId = new Guid("22222222-0000-0000-0000-000000000001");
        var mealBoxTypeId     = new Guid("22222222-0000-0000-0000-000000000002");
        var breadBagTypeId    = new Guid("22222222-0000-0000-0000-000000000003");
        var veggieBoxTypeId   = new Guid("22222222-0000-0000-0000-000000000004");
        var pastryBoxTypeId   = new Guid("22222222-0000-0000-0000-000000000005");

        modelBuilder.Entity<PackageType>().HasData(
            new PackageType { Id = surpriseBagTypeId, Name = "Surprise Bag" },
            new PackageType { Id = mealBoxTypeId,     Name = "Meal Box" },
            new PackageType { Id = breadBagTypeId,    Name = "Bread Bag" },
            new PackageType { Id = veggieBoxTypeId,   Name = "Veggie Box" },
            new PackageType { Id = pastryBoxTypeId,   Name = "Pastry Box" }
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
        var business4Id = new Guid("44444444-0000-0000-0000-000000000004");
        var business5Id = new Guid("44444444-0000-0000-0000-000000000005");
        var business6Id = new Guid("44444444-0000-0000-0000-000000000006");
        var business7Id = new Guid("44444444-0000-0000-0000-000000000007");
        var business8Id = new Guid("44444444-0000-0000-0000-000000000008");

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
            },
            new Business
            {
                Id             = business4Id,
                Name           = "La Mama",
                Description    = "Traditional Romanian home-cooking with generous portions.",
                Address        = "3 Unirii Square, Cluj-Napoca",
                BusinessTypeId = restaurantTypeId
            },
            new Business
            {
                Id             = business5Id,
                Name           = "Wheat & Wonder",
                Description    = "Family-run bakery specialising in sourdough and sweet rolls.",
                Address        = "21 Flour Street, Cluj-Napoca",
                BusinessTypeId = bakeryTypeId
            },
            new Business
            {
                Id             = business6Id,
                Name           = "Bloom & Brew",
                Description    = "Cosy cafe with seasonal drinks and homemade cakes.",
                Address        = "7 Garden Alley, Cluj-Napoca",
                BusinessTypeId = cafeTypeId
            },
            new Business
            {
                Id             = business7Id,
                Name           = "FreshMart",
                Description    = "Local grocery store with fresh produce and ready-made meals.",
                Address        = "55 Market Road, Cluj-Napoca",
                BusinessTypeId = groceryTypeId
            },
            new Business
            {
                Id             = business8Id,
                Name           = "Street Spoon",
                Description    = "Food truck serving global street-food with a local twist.",
                Address        = "Central Park, Cluj-Napoca",
                BusinessTypeId = foodTruckTypeId
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
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000005"),
                BusinessId    = business4Id,
                PackageTypeId = mealBoxTypeId,
                Name          = "La Mama Meal Box",
                Description   = "A hearty Romanian meal with soup, main, and dessert.",
                Price         = 11.50m,
                Quantity      = 6,
                PickupStart   = new DateTime(2026, 7, 4, 17, 30, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 20, 30, 0, DateTimeKind.Utc)
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000006"),
                BusinessId    = business4Id,
                PackageTypeId = surpriseBagTypeId,
                Name          = "La Mama Surprise Bag",
                Description   = "Mystery mix of leftover homemade Romanian dishes.",
                Price         = 8.50m,
                Quantity      = 4,
                PickupStart   = new DateTime(2026, 7, 4, 19, 0, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 21, 30, 0, DateTimeKind.Utc)
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000007"),
                BusinessId    = business5Id,
                PackageTypeId = pastryBoxTypeId,
                Name          = "Wheat & Wonder Pastry Box",
                Description   = "Six assorted pastries: croissants, cinnamon rolls, and more.",
                Price         = 8.00m,
                Quantity      = 8,
                PickupStart   = new DateTime(2026, 7, 4, 15, 0, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 18, 0, 0, DateTimeKind.Utc)
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000008"),
                BusinessId    = business5Id,
                PackageTypeId = breadBagTypeId,
                Name          = "Wheat & Wonder Bread Bag",
                Description   = "End-of-day selection of sourdough, rye, and whole wheat loaves.",
                Price         = 5.00m,
                Quantity      = 12,
                PickupStart   = new DateTime(2026, 7, 4, 16, 30, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 19, 30, 0, DateTimeKind.Utc)
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000009"),
                BusinessId    = business6Id,
                PackageTypeId = surpriseBagTypeId,
                Name          = "Bloom & Brew Surprise Bag",
                Description   = "Leftover cakes, cookies, and quiches of the day.",
                Price         = 6.99m,
                Quantity      = 5,
                PickupStart   = new DateTime(2026, 7, 4, 17, 0, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 19, 0, 0, DateTimeKind.Utc)
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000010"),
                BusinessId    = business7Id,
                PackageTypeId = veggieBoxTypeId,
                Name          = "FreshMart Veggie Box",
                Description   = "Seasonal vegetables and fruit nearing best-before — still fresh.",
                Price         = 5.50m,
                Quantity      = 15,
                PickupStart   = new DateTime(2026, 7, 4, 16, 0, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 20, 0, 0, DateTimeKind.Utc)
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000011"),
                BusinessId    = business8Id,
                PackageTypeId = mealBoxTypeId,
                Name          = "Street Spoon Meal Box",
                Description   = "A full street-food meal — tacos, gyros, or noodles (surprise pick).",
                Price         = 10.00m,
                Quantity      = 7,
                PickupStart   = new DateTime(2026, 7, 4, 18, 30, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 21, 0, 0, DateTimeKind.Utc)
            },
            new Package
            {
                Id            = new Guid("55555555-0000-0000-0000-000000000012"),
                BusinessId    = business8Id,
                PackageTypeId = surpriseBagTypeId,
                Name          = "Street Spoon Surprise Bag",
                Description   = "Mixed snacks, sides, and small bites leftover from the day.",
                Price         = 5.99m,
                Quantity      = 6,
                PickupStart   = new DateTime(2026, 7, 4, 20, 0, 0, DateTimeKind.Utc),
                PickupEnd     = new DateTime(2026, 7, 4, 22, 0, 0, DateTimeKind.Utc)
            }
        );
    }
}