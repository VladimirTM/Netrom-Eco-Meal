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
        var restaurant = new Guid("11111111-0000-0000-0000-000000000001");
        var bakery     = new Guid("11111111-0000-0000-0000-000000000002");
        var cafe       = new Guid("11111111-0000-0000-0000-000000000003");
        var grocery    = new Guid("11111111-0000-0000-0000-000000000004");
        var foodTruck  = new Guid("11111111-0000-0000-0000-000000000005");

        // World Cup–themed businesses, all based in Timișoara.
        var seedBusinesses = new List<Business>
        {
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000001"), Name = "Stadionul de Gusturi", Description = "Matchday feasts inspired by World Cup host cities, made from the day's surplus.",       Address = "Bulevardul Revoluției 1989 10, Timișoara", BusinessTypeId = restaurant, ImageUrl = "https://loremflickr.com/640/400/soccer,stadium/all?lock=201" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000002"), Name = "VAR Bistro",           Description = "Reviewing yesterday's dishes so nothing goes offside — or to waste.",                    Address = "Bulevardul Take Ionescu 56, Timișoara",    BusinessTypeId = restaurant, ImageUrl = "https://loremflickr.com/640/400/football,referee/all?lock=202" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000003"), Name = "Derby Deli",           Description = "Home-cooked rivalries: hearty plates from Timișoara's derby-day kitchens.",              Address = "Strada Coriolan Brediceanu 3, Timișoara",  BusinessTypeId = restaurant, ImageUrl = "https://loremflickr.com/640/400/football,derby/all?lock=203" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000004"), Name = "Poarta de Aur Bakery", Description = "Golden-goal bread and pastries fresh off the bench every morning.",                      Address = "Piața Unirii 4, Timișoara",                 BusinessTypeId = bakery,     ImageUrl = "https://loremflickr.com/640/400/football,goal/all?lock=204" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000005"), Name = "Hat-Trick Bakery",     Description = "Three fresh batches a day: bread, pastries, and match-day pretzels.",                    Address = "Strada Vasile Alecsandri 14, Timișoara",   BusinessTypeId = bakery,     ImageUrl = "https://loremflickr.com/640/400/football,trophy/all?lock=205" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000006"), Name = "Fotbal & Focaccia",    Description = "Bakery-cafe hybrid baking focaccia and finalist-worthy pastries.",                       Address = "Piața Libertății 7, Timișoara",             BusinessTypeId = bakery,     ImageUrl = "https://loremflickr.com/640/400/football,worldcup/all?lock=206" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000007"), Name = "Extra Time Café",      Description = "Coffee and snacks for those who go into overtime.",                                       Address = "Strada Alba Iulia 22, Timișoara",           BusinessTypeId = cafe,       ImageUrl = "https://loremflickr.com/640/400/football,fans/all?lock=207" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000008"), Name = "Cartonaș Galben Café", Description = "Coffee strong enough to earn a caution.",                                                 Address = "Piața Victoriei 2, Timișoara",              BusinessTypeId = cafe,       ImageUrl = "https://loremflickr.com/640/400/football,jersey/all?lock=208" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000009"), Name = "Fault Fresh Market",   Description = "Produce nearing its final whistle — still match-fit.",                                   Address = "Calea Aradului 33, Timișoara",              BusinessTypeId = grocery,    ImageUrl = "https://loremflickr.com/640/400/soccer,ball/all?lock=209" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000010"), Name = "Penalty Pantry",      Description = "Surplus groceries saved before they're sent off.",                                        Address = "Calea Șagului 88, Timișoara",               BusinessTypeId = grocery,    ImageUrl = "https://loremflickr.com/640/400/football,penalty/all?lock=210" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000011"), Name = "Food Truck Mundial",  Description = "Street food from every World Cup host nation, one truck at a time.",                      Address = "Parcul Rozelor, Timișoara",                 BusinessTypeId = foodTruck,  ImageUrl = "https://loremflickr.com/640/400/worldcup,streetfood/all?lock=211" },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000012"), Name = "Fan Zone Grill",      Description = "Grilled street food straight from the fan zone.",                                          Address = "Bulevardul Republicii 5, Timișoara",        BusinessTypeId = foodTruck,  ImageUrl = "https://loremflickr.com/640/400/football,grill/all?lock=212" },
        };

        // Add missing seed rows and refresh stale placeholder images, without touching an
        // admin-customized image.
        var seedIds = seedBusinesses.Select(b => b.Id).ToList();
        var existingById = await db.Businesses
            .Where(b => seedIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id);

        foreach (var seed in seedBusinesses)
        {
            if (!existingById.TryGetValue(seed.Id, out var existing))
            {
                db.Businesses.Add(seed);
            }
            else if (IsStalePlaceholderImage(existing.ImageUrl))
            {
                existing.ImageUrl = seed.ImageUrl;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedPackagesAsync(EcoMealDbContext db)
    {
        var b1  = new Guid("44444444-0000-0000-0000-000000000001");
        var b2  = new Guid("44444444-0000-0000-0000-000000000002");
        var b3  = new Guid("44444444-0000-0000-0000-000000000003");
        var b4  = new Guid("44444444-0000-0000-0000-000000000004");
        var b5  = new Guid("44444444-0000-0000-0000-000000000005");
        var b6  = new Guid("44444444-0000-0000-0000-000000000006");
        var b7  = new Guid("44444444-0000-0000-0000-000000000007");
        var b8  = new Guid("44444444-0000-0000-0000-000000000008");
        var b9  = new Guid("44444444-0000-0000-0000-000000000009");
        var b10 = new Guid("44444444-0000-0000-0000-000000000010");
        var b11 = new Guid("44444444-0000-0000-0000-000000000011");
        var b12 = new Guid("44444444-0000-0000-0000-000000000012");

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
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000001"), BusinessId = b1,  PackageTypeId = surpriseBag, Name = "Golden Boot Surprise Bag",     Description = "A top-scoring surprise selection of today's leftover dishes.",              Price = 12.99m, Quantity = 5,  PickupStart = At(17,  0), PickupEnd = At(20,  0), ImageUrl = "https://loremflickr.com/640/360/football,goldenboot/all?lock=301" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000002"), BusinessId = b1,  PackageTypeId = mealBox,     Name = "Final Whistle Meal Box",       Description = "A full meal box with a main course and side dish, boxed at full-time.",     Price =  9.99m, Quantity = 3,  PickupStart = At(17,  0), PickupEnd = At(20,  0), ImageUrl = "https://loremflickr.com/640/360/football,whistle/all?lock=302" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000003"), BusinessId = b2,  PackageTypeId = surpriseBag, Name = "Offside Surprise Bag",         Description = "Leftover plates that were flagged for the bench — still perfectly good.",  Price =  8.99m, Quantity = 4,  PickupStart = At(18,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,offside/all?lock=303" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000004"), BusinessId = b2,  PackageTypeId = mealBox,     Name = "Extra Time Meal Box",          Description = "A hearty meal for the ones who always stay till the final minute.",        Price = 10.50m, Quantity = 5,  PickupStart = At(19,  0), PickupEnd = At(22,  0), ImageUrl = "https://loremflickr.com/640/360/football,overtime/all?lock=304" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000005"), BusinessId = b3,  PackageTypeId = surpriseBag, Name = "Derby Day Surprise Bag",       Description = "A mystery mix of leftover homemade Romanian dishes, derby-day style.",     Price =  8.50m, Quantity = 4,  PickupStart = At(19,  0), PickupEnd = At(21, 30), ImageUrl = "https://loremflickr.com/640/360/football,derby/all?lock=305" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000006"), BusinessId = b3,  PackageTypeId = mealBox,     Name = "Hat-Trick Meal Box",           Description = "Soup, main, and dessert — a three-course hat-trick in one box.",           Price = 11.50m, Quantity = 6,  PickupStart = At(17, 30), PickupEnd = At(20, 30), ImageUrl = "https://loremflickr.com/640/360/football,hattrick/all?lock=306" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000007"), BusinessId = b4,  PackageTypeId = breadBag,    Name = "Golden Goal Bread Bag",        Description = "Assorted fresh breads and pastries from the day, straight off the bench.", Price =  6.50m, Quantity = 10, PickupStart = At(16,  0), PickupEnd = At(19,  0), ImageUrl = "https://loremflickr.com/640/360/football,goal/all?lock=307" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000008"), BusinessId = b4,  PackageTypeId = pastryBox,   Name = "Trophy Pastry Box",            Description = "Six championship-worthy pastries: croissants, cinnamon rolls, and more.",  Price =  8.00m, Quantity = 8,  PickupStart = At(15,  0), PickupEnd = At(18,  0), ImageUrl = "https://loremflickr.com/640/360/football,trophy/all?lock=308" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000009"), BusinessId = b5,  PackageTypeId = breadBag,    Name = "Kick-Off Bread Bag",           Description = "End-of-day selection of sourdough, rye, and whole wheat loaves.",          Price =  5.00m, Quantity = 12, PickupStart = At(16, 30), PickupEnd = At(19, 30), ImageUrl = "https://loremflickr.com/640/360/soccer,kickoff/all?lock=309" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000010"), BusinessId = b5,  PackageTypeId = pastryBox,   Name = "Penalty Pastry Box",           Description = "A last-minute lineup of leftover match-day pretzels and sweet rolls.",     Price =  7.50m, Quantity = 8,  PickupStart = At(15, 30), PickupEnd = At(18, 30), ImageUrl = "https://loremflickr.com/640/360/football,penalty/all?lock=310" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000011"), BusinessId = b6,  PackageTypeId = breadBag,    Name = "Corner Kick Bread Bag",        Description = "Fresh focaccia and bread ends, curved in fresh like a corner kick.",       Price =  5.50m, Quantity = 10, PickupStart = At(16,  0), PickupEnd = At(19,  0), ImageUrl = "https://loremflickr.com/640/360/football,corner/all?lock=311" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000012"), BusinessId = b6,  PackageTypeId = pastryBox,   Name = "Champions Pastry Box",         Description = "A finalist's assortment of the day's best leftover pastries.",             Price =  8.50m, Quantity = 6,  PickupStart = At(15,  0), PickupEnd = At(18,  0), ImageUrl = "https://loremflickr.com/640/360/football,champions/all?lock=312" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000013"), BusinessId = b7,  PackageTypeId = surpriseBag, Name = "Half-Time Surprise Bag",       Description = "Leftover sandwiches, muffins, and snacks from the cafe.",                  Price =  7.99m, Quantity = 4,  PickupStart = At(18,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,halftime/all?lock=313" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000014"), BusinessId = b7,  PackageTypeId = pastryBox,   Name = "Stoppage Time Pastry Box",     Description = "The last few pastries added on before the counter closes for the day.",    Price =  6.99m, Quantity = 5,  PickupStart = At(19,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,stoppagetime/all?lock=314" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000015"), BusinessId = b8,  PackageTypeId = surpriseBag, Name = "Yellow Card Surprise Bag",     Description = "Leftover cakes, cookies, and quiches of the day — a caution against waste.",Price =  6.99m, Quantity = 5,  PickupStart = At(17,  0), PickupEnd = At(19,  0), ImageUrl = "https://loremflickr.com/640/360/football,yellowcard/all?lock=315" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000016"), BusinessId = b8,  PackageTypeId = pastryBox,   Name = "Red Card Pastry Box",          Description = "Dark chocolate and espresso pastries, sent off before they go stale.",     Price =  7.25m, Quantity = 5,  PickupStart = At(17, 30), PickupEnd = At(19, 30), ImageUrl = "https://loremflickr.com/640/360/football,redcard/all?lock=316" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000017"), BusinessId = b9,  PackageTypeId = veggieBox,   Name = "Offside Veggie Box",           Description = "Seasonal vegetables and fruit nearing best-before — still match-fit.",     Price =  5.50m, Quantity = 15, PickupStart = At(16,  0), PickupEnd = At(20,  0), ImageUrl = "https://loremflickr.com/640/360/vegetables,market/all?lock=317" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000018"), BusinessId = b9,  PackageTypeId = veggieBox,   Name = "Injury Time Veggie Box",       Description = "A last-minute rescue of fresh produce before it's subbed off the shelf.",  Price =  5.00m, Quantity = 12, PickupStart = At(19,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/vegetables,fresh/all?lock=318" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000019"), BusinessId = b10, PackageTypeId = veggieBox,   Name = "Penalty Box Veggie Box",       Description = "Surplus greens and fruit saved right from the penalty box.",              Price =  5.75m, Quantity = 10, PickupStart = At(16, 30), PickupEnd = At(19, 30), ImageUrl = "https://loremflickr.com/640/360/football,penaltybox/all?lock=319" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000020"), BusinessId = b10, PackageTypeId = mealBox,     Name = "Added Time Meal Box",          Description = "A ready-made meal box rescued in the final added minutes of the day.",     Price =  9.50m, Quantity = 6,  PickupStart = At(18,  0), PickupEnd = At(20, 30), ImageUrl = "https://loremflickr.com/640/360/football,addedtime/all?lock=320" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000021"), BusinessId = b11, PackageTypeId = mealBox,     Name = "World Cup Meal Box",           Description = "A full street-food meal inspired by host nations — tacos, gyros, or noodles.",Price = 10.00m, Quantity = 7,  PickupStart = At(18, 30), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/worldcup,streetfood/all?lock=321" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000022"), BusinessId = b11, PackageTypeId = surpriseBag, Name = "Fan Zone Surprise Bag",        Description = "Mixed snacks, sides, and small bites leftover from the day's service.",    Price =  5.99m, Quantity = 6,  PickupStart = At(20,  0), PickupEnd = At(22,  0), ImageUrl = "https://loremflickr.com/640/360/football,fanzone/all?lock=322" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000023"), BusinessId = b12, PackageTypeId = mealBox,     Name = "Top Scorer Meal Box",          Description = "The crowd favourite: a grilled meal box that scores every time.",          Price = 10.50m, Quantity = 6,  PickupStart = At(18,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,grill/all?lock=323" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000024"), BusinessId = b12, PackageTypeId = surpriseBag, Name = "Stadium Snack Surprise Bag",   Description = "A grab-bag of grilled snacks and sides fresh from the fan zone grill.",    Price =  6.25m, Quantity = 8,  PickupStart = At(19,  0), PickupEnd = At(21, 30), ImageUrl = "https://loremflickr.com/640/360/football,snacks/all?lock=324" }
        };

        // Refresh only the pickup window (not quantity, which reflects real orders) for known
        // seed packages once it's expired, instead of re-inserting or touching admin-added ones.
        // Also refreshes stale placeholder images, same as SeedBusinessesAsync above.
        var seedIds = seedPackages.Select(p => p.Id).ToList();
        var existingById = await db.Packages
            .Where(p => seedIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var seed in seedPackages)
        {
            if (!existingById.TryGetValue(seed.Id, out var existing))
            {
                db.Packages.Add(seed);
                continue;
            }

            if (existing.PickupEnd < DateTime.UtcNow)
            {
                existing.PickupStart = seed.PickupStart;
                existing.PickupEnd = seed.PickupEnd;
            }

            if (IsStalePlaceholderImage(existing.ImageUrl))
            {
                existing.ImageUrl = seed.ImageUrl;
            }
        }

        await db.SaveChangesAsync();
    }

    // True when there's no image, or it's on a placeholder host we've retired (picsum.photos) —
    // not loremflickr.com, which is also the current seed default and could be an admin's own choice.
    private static bool IsStalePlaceholderImage(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) || imageUrl.Contains("picsum.photos");
}