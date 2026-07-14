using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Database;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));

        foreach (var role in AppRoles.AllRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var adminEmail = configuration["SeedAdmin:Email"];
        var adminPassword = configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("SeedAdmin:Email/SeedAdmin:Password are not configured — no admin account will be seeded.");
        }
        else
        {
            var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
            if (existingAdmin is null)
            {
                var admin = new ApplicationUser
                {
                    Name = "Admin",
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                };
                var result = await userManager.CreateAsync(admin, adminPassword);

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, AppRoles.Admin);
                }
                else
                {
                    logger.LogWarning("Failed to seed admin account {Email}: {Errors}", adminEmail,
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        var db = services.GetRequiredService<EcoMealDbContext>();
        await SeedBusinessTypesAsync(db);
        await SeedPackageTypesAsync(db);
        await SeedStatusesAsync(db);
        await SeedBusinessesAsync(db);
        await SeedPackagesAsync(db);
    }

    private static async Task SeedBusinessTypesAsync(EcoMealDbContext db)
    {
        if (await db.BusinessTypes.AnyAsync()) return;

        db.BusinessTypes.AddRange(
            new BusinessType { Id = new Guid("11111111-0000-0000-0000-000000000001"), Name = "Restaurant" },
            new BusinessType { Id = new Guid("11111111-0000-0000-0000-000000000002"), Name = "Bakery" },
            new BusinessType { Id = new Guid("11111111-0000-0000-0000-000000000003"), Name = "Cafe" },
            new BusinessType { Id = new Guid("11111111-0000-0000-0000-000000000004"), Name = "Grocery Store" },
            new BusinessType { Id = new Guid("11111111-0000-0000-0000-000000000005"), Name = "Food Truck" }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedPackageTypesAsync(EcoMealDbContext db)
    {
        if (await db.PackageTypes.AnyAsync()) return;

        db.PackageTypes.AddRange(
            new PackageType { Id = new Guid("22222222-0000-0000-0000-000000000001"), Name = "Surprise Bag" },
            new PackageType { Id = new Guid("22222222-0000-0000-0000-000000000002"), Name = "Meal Box" },
            new PackageType { Id = new Guid("22222222-0000-0000-0000-000000000003"), Name = "Bread Bag" },
            new PackageType { Id = new Guid("22222222-0000-0000-0000-000000000004"), Name = "Veggie Box" },
            new PackageType { Id = new Guid("22222222-0000-0000-0000-000000000005"), Name = "Pastry Box" }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedStatusesAsync(EcoMealDbContext db)
    {
        if (await db.Statuses.AnyAsync()) return;

        db.Statuses.AddRange(
            new Status { Id = new Guid("33333333-0000-0000-0000-000000000001"), Name = OrderStatuses.Pending },
            new Status { Id = new Guid("33333333-0000-0000-0000-000000000002"), Name = OrderStatuses.Confirmed },
            new Status { Id = new Guid("33333333-0000-0000-0000-000000000003"), Name = OrderStatuses.Completed },
            new Status { Id = new Guid("33333333-0000-0000-0000-000000000004"), Name = OrderStatuses.Cancelled }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedBusinessesAsync(EcoMealDbContext db)
    {
        if (await db.Businesses.AnyAsync()) return;

        var restaurant = new Guid("11111111-0000-0000-0000-000000000001");
        var bakery     = new Guid("11111111-0000-0000-0000-000000000002");
        var cafe       = new Guid("11111111-0000-0000-0000-000000000003");
        var grocery    = new Guid("11111111-0000-0000-0000-000000000004");
        var foodTruck  = new Guid("11111111-0000-0000-0000-000000000005");

        db.Businesses.AddRange(
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000001"), Name = "Green Fork",      Description = "Farm-to-table restaurant with daily surplus meals.",             Address = "12 Oak Street, Cluj-Napoca",       BusinessTypeId = restaurant, ImageUrl = "https://picsum.photos/seed/green-fork/640/400" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000002"), Name = "Sunrise Bakery",  Description = "Artisan bakery with fresh bread and pastries every morning.",   Address = "5 Bread Lane, Cluj-Napoca",         BusinessTypeId = bakery, ImageUrl = "https://picsum.photos/seed/sunrise-bakery/640/400" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000003"), Name = "The Daily Grind", Description = "Specialty coffee shop with homemade snacks.",                  Address = "88 Central Boulevard, Cluj-Napoca", BusinessTypeId = cafe, ImageUrl = "https://picsum.photos/seed/daily-grind/640/400" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000004"), Name = "La Mama",         Description = "Traditional Romanian home-cooking with generous portions.",     Address = "3 Unirii Square, Cluj-Napoca",      BusinessTypeId = restaurant, ImageUrl = "https://picsum.photos/seed/la-mama/640/400" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000005"), Name = "Wheat & Wonder",  Description = "Family-run bakery specialising in sourdough and sweet rolls.", Address = "21 Flour Street, Cluj-Napoca",      BusinessTypeId = bakery, ImageUrl = "https://picsum.photos/seed/wheat-wonder/640/400" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000006"), Name = "Bloom & Brew",    Description = "Cosy cafe with seasonal drinks and homemade cakes.",           Address = "7 Garden Alley, Cluj-Napoca",       BusinessTypeId = cafe, ImageUrl = "https://picsum.photos/seed/bloom-brew/640/400" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000007"), Name = "FreshMart",       Description = "Local grocery store with fresh produce and ready-made meals.", Address = "55 Market Road, Cluj-Napoca",       BusinessTypeId = grocery, ImageUrl = "https://picsum.photos/seed/freshmart/640/400" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000008"), Name = "Street Spoon",    Description = "Food truck serving global street-food with a local twist.",    Address = "Central Park, Cluj-Napoca",         BusinessTypeId = foodTruck, ImageUrl = "https://picsum.photos/seed/street-spoon/640/400" }
        );
        await db.SaveChangesAsync();
    }

    private static async Task SeedPackagesAsync(EcoMealDbContext db)
    {
        var b1 = new Guid("44444444-0000-0000-0000-000000000001");
        var b2 = new Guid("44444444-0000-0000-0000-000000000002");
        var b3 = new Guid("44444444-0000-0000-0000-000000000003");
        var b4 = new Guid("44444444-0000-0000-0000-000000000004");
        var b5 = new Guid("44444444-0000-0000-0000-000000000005");
        var b6 = new Guid("44444444-0000-0000-0000-000000000006");
        var b7 = new Guid("44444444-0000-0000-0000-000000000007");
        var b8 = new Guid("44444444-0000-0000-0000-000000000008");

        var surpriseBag = new Guid("22222222-0000-0000-0000-000000000001");
        var mealBox     = new Guid("22222222-0000-0000-0000-000000000002");
        var breadBag    = new Guid("22222222-0000-0000-0000-000000000003");
        var veggieBox   = new Guid("22222222-0000-0000-0000-000000000004");
        var pastryBox   = new Guid("22222222-0000-0000-0000-000000000005");

        // Anchored to "today" so the storefront always opens with live, orderable packages.
        var today = DateTime.UtcNow.Date;
        DateTime At(int hour, int minute) => today.AddHours(hour).AddMinutes(minute);

        var seedPackages = new List<Package>
        {
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000001"), BusinessId = b1, PackageTypeId = surpriseBag, Name = "Green Fork Surprise Bag",   Description = "A surprise selection of today's leftover dishes — always delicious.",   Price = 12.99m, Quantity = 5,  PickupStart = At(17,  0), PickupEnd = At(20,  0), ImageUrl = "https://picsum.photos/seed/green-fork-surprise-bag/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000002"), BusinessId = b1, PackageTypeId = mealBox,     Name = "Green Fork Meal Box",       Description = "A full meal box with a main course and side dish.",                    Price =  9.99m, Quantity = 3,  PickupStart = At(17,  0), PickupEnd = At(20,  0), ImageUrl = "https://picsum.photos/seed/green-fork-meal-box/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000003"), BusinessId = b2, PackageTypeId = breadBag,    Name = "Sunrise Bread Bag",         Description = "Assorted fresh breads and pastries from the day.",                    Price =  6.50m, Quantity = 10, PickupStart = At(16,  0), PickupEnd = At(19,  0), ImageUrl = "https://picsum.photos/seed/sunrise-bread-bag/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000004"), BusinessId = b3, PackageTypeId = surpriseBag, Name = "Daily Grind Surprise Bag",  Description = "Leftover sandwiches, muffins, and snacks from the cafe.",             Price =  7.99m, Quantity = 4,  PickupStart = At(18,  0), PickupEnd = At(21,  0), ImageUrl = "https://picsum.photos/seed/daily-grind-surprise-bag/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000005"), BusinessId = b4, PackageTypeId = mealBox,     Name = "La Mama Meal Box",          Description = "A hearty Romanian meal with soup, main, and dessert.",                Price = 11.50m, Quantity = 6,  PickupStart = At(17, 30), PickupEnd = At(20, 30), ImageUrl = "https://picsum.photos/seed/la-mama-meal-box/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000006"), BusinessId = b4, PackageTypeId = surpriseBag, Name = "La Mama Surprise Bag",      Description = "Mystery mix of leftover homemade Romanian dishes.",                   Price =  8.50m, Quantity = 4,  PickupStart = At(19,  0), PickupEnd = At(21, 30), ImageUrl = "https://picsum.photos/seed/la-mama-surprise-bag/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000007"), BusinessId = b5, PackageTypeId = pastryBox,   Name = "Wheat & Wonder Pastry Box", Description = "Six assorted pastries: croissants, cinnamon rolls, and more.",        Price =  8.00m, Quantity = 8,  PickupStart = At(15,  0), PickupEnd = At(18,  0), ImageUrl = "https://picsum.photos/seed/wheat-wonder-pastry-box/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000008"), BusinessId = b5, PackageTypeId = breadBag,    Name = "Wheat & Wonder Bread Bag",  Description = "End-of-day selection of sourdough, rye, and whole wheat loaves.",     Price =  5.00m, Quantity = 12, PickupStart = At(16, 30), PickupEnd = At(19, 30), ImageUrl = "https://picsum.photos/seed/wheat-wonder-bread-bag/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000009"), BusinessId = b6, PackageTypeId = surpriseBag, Name = "Bloom & Brew Surprise Bag", Description = "Leftover cakes, cookies, and quiches of the day.",                    Price =  6.99m, Quantity = 5,  PickupStart = At(17,  0), PickupEnd = At(19,  0), ImageUrl = "https://picsum.photos/seed/bloom-brew-surprise-bag/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000010"), BusinessId = b7, PackageTypeId = veggieBox,   Name = "FreshMart Veggie Box",      Description = "Seasonal vegetables and fruit nearing best-before — still fresh.",    Price =  5.50m, Quantity = 15, PickupStart = At(16,  0), PickupEnd = At(20,  0), ImageUrl = "https://picsum.photos/seed/freshmart-veggie-box/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000011"), BusinessId = b8, PackageTypeId = mealBox,     Name = "Street Spoon Meal Box",     Description = "A full street-food meal — tacos, gyros, or noodles (surprise pick).",Price = 10.00m, Quantity = 7,  PickupStart = At(18, 30), PickupEnd = At(21,  0), ImageUrl = "https://picsum.photos/seed/street-spoon-meal-box/640/360" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000012"), BusinessId = b8, PackageTypeId = surpriseBag, Name = "Street Spoon Surprise Bag", Description = "Mixed snacks, sides, and small bites leftover from the day.",         Price =  5.99m, Quantity = 6,  PickupStart = At(20,  0), PickupEnd = At(22,  0), ImageUrl = "https://picsum.photos/seed/street-spoon-surprise-bag/640/360" }
        };

        // Refresh only the pickup window (not quantity, which reflects real orders) for known
        // seed packages once it's expired, instead of re-inserting or touching admin-added ones.
        var seedIds = seedPackages.Select(p => p.Id).ToList();
        var existingById = await db.Packages
            .Where(p => seedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var seed in seedPackages)
        {
            if (!existingById.TryGetValue(seed.Id, out var existing))
            {
                db.Packages.Add(seed);
            }
            else if (existing.PickupEnd < DateTime.UtcNow)
            {
                existing.PickupStart = seed.PickupStart;
                existing.PickupEnd = seed.PickupEnd;
            }
        }

        await db.SaveChangesAsync();
    }
}