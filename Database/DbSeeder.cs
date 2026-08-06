using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Database;

public static class DbSeeder
{
    // Fixed demo credentials so every feature (dashboard trends, reorder, favorites, reviews,
    // the notification bell, QR pickup...) has something to look at without extra config. Not
    // real accounts, so — unlike SeedAdmin — no reason to gate these behind configuration.
    private const string DemoCustomerEmail = "demo.customer@ecomeal.local";
    private const string DemoCustomerPassword = "Demo123!";
    private const string DemoManagerEmail = "demo.manager@ecomeal.local";
    private const string DemoManagerPassword = "Demo123!";

    // The managed business for the demo manager/orders below — Stadionul de Gusturi.
    private static readonly Guid DemoManagedBusinessId = new("44444444-0000-0000-0000-000000000001");

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
            await GetOrCreateUserAsync(userManager, adminEmail, "Admin", AppRoles.Admin, adminPassword, logger);
        }

        var demoCustomer = await GetOrCreateUserAsync(userManager, DemoCustomerEmail, "Demo Customer", AppRoles.Customer, DemoCustomerPassword, logger);
        var demoManager = await GetOrCreateUserAsync(userManager, DemoManagerEmail, "Demo Manager", AppRoles.BusinessManager, DemoManagerPassword, logger);

        var db = services.GetRequiredService<EcoMealDbContext>();
        await SeedBusinessTypesAsync(db);
        await SeedPackageTypesAsync(db);
        await SeedStatusesAsync(db);
        await SeedBusinessesAsync(db);
        await SeedPackagesAsync(db);
        await SeedPackageTemplateAsync(db);

        if (demoCustomer is not null && demoManager is not null)
            await SeedDemoActivityAsync(db, demoCustomer, demoManager.Id);
    }

    // Shared by the admin account above and the demo customer/manager below — same
    // find-or-create-and-assign-role shape, only the role and idempotency trigger differ.
    private static async Task<ApplicationUser?> GetOrCreateUserAsync(
        UserManager<ApplicationUser> userManager, string email, string name, string role, string password, ILogger logger)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return existing;

        var user = new ApplicationUser { Name = name, UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            logger.LogWarning("Failed to seed account {Email}: {Errors}", email,
                string.Join("; ", result.Errors.Select(e => e.Description)));
            return null;
        }

        await userManager.AddToRoleAsync(user, role);
        return user;
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

    // Adds any status row that's missing rather than bailing out when the table is non-empty — an
    // old migration (pre-DbSeeder) hardcoded InsertData for the original four statuses, so a fresh
    // database already has those by the time this runs, and a blanket "if (Any) return" would
    // silently skip NoShow forever. See BACKEND_ARCHITECTURE.md §9 for the migration-vs-seeder history.
    private static async Task SeedStatusesAsync(EcoMealDbContext db)
    {
        var allStatuses = new List<Status>
        {
            new() { Id = new Guid("33333333-0000-0000-0000-000000000001"), Name = OrderStatuses.Pending },
            new() { Id = new Guid("33333333-0000-0000-0000-000000000002"), Name = OrderStatuses.Confirmed },
            new() { Id = new Guid("33333333-0000-0000-0000-000000000003"), Name = OrderStatuses.Completed },
            new() { Id = new Guid("33333333-0000-0000-0000-000000000004"), Name = OrderStatuses.Cancelled },
            new() { Id = new Guid("33333333-0000-0000-0000-000000000005"), Name = OrderStatuses.NoShow },
        };

        var existingNames = await db.Statuses.Select(s => s.Name).ToListAsync();
        var missing = allStatuses.Where(s => !existingNames.Contains(s.Name)).ToList();
        if (missing.Count == 0) return;

        db.Statuses.AddRange(missing);
        await db.SaveChangesAsync();
    }

    private static async Task SeedBusinessesAsync(EcoMealDbContext db)
    {
        var restaurant = new Guid("11111111-0000-0000-0000-000000000001");
        var bakery     = new Guid("11111111-0000-0000-0000-000000000002");
        var cafe       = new Guid("11111111-0000-0000-0000-000000000003");
        var grocery    = new Guid("11111111-0000-0000-0000-000000000004");
        var foodTruck  = new Guid("11111111-0000-0000-0000-000000000005");

        // World Cup–themed businesses, all based in Timișoara. Coordinates are approximate
        // street/square estimates — good enough to demo distance sort and the map view.
        var seedBusinesses = new List<Business>
        {
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000001"), Name = "Stadionul de Gusturi", Description = "Matchday feasts inspired by World Cup host cities, made from the day's surplus.",       Address = "Bulevardul Revoluției 1989 10, Timișoara", BusinessTypeId = restaurant, ImageUrl = "https://loremflickr.com/640/400/soccer,stadium/all?lock=201", Latitude = 45.7556, Longitude = 21.2280 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000002"), Name = "VAR Bistro",           Description = "Reviewing yesterday's dishes so nothing goes offside — or to waste.",                    Address = "Bulevardul Take Ionescu 56, Timișoara",    BusinessTypeId = restaurant, ImageUrl = "https://loremflickr.com/640/400/football,referee/all?lock=202", Latitude = 45.7531, Longitude = 21.2352 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000003"), Name = "Derby Deli",           Description = "Home-cooked rivalries: hearty plates from Timișoara's derby-day kitchens.",              Address = "Strada Coriolan Brediceanu 3, Timișoara",  BusinessTypeId = restaurant, ImageUrl = "https://loremflickr.com/640/400/football,derby/all?lock=203", Latitude = 45.7492, Longitude = 21.2231 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000004"), Name = "Poarta de Aur Bakery", Description = "Golden-goal bread and pastries fresh off the bench every morning.",                      Address = "Piața Unirii 4, Timișoara",                 BusinessTypeId = bakery,     ImageUrl = "https://loremflickr.com/640/400/football,goal/all?lock=204", Latitude = 45.7579, Longitude = 21.2233 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000005"), Name = "Hat-Trick Bakery",     Description = "Three fresh batches a day: bread, pastries, and match-day pretzels.",                    Address = "Strada Vasile Alecsandri 14, Timișoara",   BusinessTypeId = bakery,     ImageUrl = "https://loremflickr.com/640/400/football,trophy/all?lock=205", Latitude = 45.7512, Longitude = 21.2202 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000006"), Name = "Fotbal & Focaccia",    Description = "Bakery-cafe hybrid baking focaccia and finalist-worthy pastries.",                       Address = "Piața Libertății 7, Timișoara",             BusinessTypeId = bakery,     ImageUrl = "https://loremflickr.com/640/400/football,worldcup/all?lock=206", Latitude = 45.7557, Longitude = 21.2247 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000007"), Name = "Extra Time Café",      Description = "Coffee and snacks for those who go into overtime.",                                       Address = "Strada Alba Iulia 22, Timișoara",           BusinessTypeId = cafe,       ImageUrl = "https://loremflickr.com/640/400/football,fans/all?lock=207", Latitude = 45.7462, Longitude = 21.2282 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000008"), Name = "Cartonaș Galben Café", Description = "Coffee strong enough to earn a caution.",                                                 Address = "Piața Victoriei 2, Timișoara",              BusinessTypeId = cafe,       ImageUrl = "https://loremflickr.com/640/400/football,jersey/all?lock=208", Latitude = 45.7539, Longitude = 21.2258 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000009"), Name = "Fault Fresh Market",   Description = "Produce nearing its final whistle — still match-fit.",                                   Address = "Calea Aradului 33, Timișoara",              BusinessTypeId = grocery,    ImageUrl = "https://loremflickr.com/640/400/soccer,ball/all?lock=209", Latitude = 45.7682, Longitude = 21.2012 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000010"), Name = "Penalty Pantry",      Description = "Surplus groceries saved before they're sent off.",                                        Address = "Calea Șagului 88, Timișoara",               BusinessTypeId = grocery,    ImageUrl = "https://loremflickr.com/640/400/football,penalty/all?lock=210", Latitude = 45.7282, Longitude = 21.1952 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000011"), Name = "Food Truck Mundial",  Description = "Street food from every World Cup host nation, one truck at a time.",                      Address = "Parcul Rozelor, Timișoara",                 BusinessTypeId = foodTruck,  ImageUrl = "https://loremflickr.com/640/400/worldcup,streetfood/all?lock=211", Latitude = 45.7461, Longitude = 21.2352 },
            new Business { Id = new Guid("44444444-0000-0000-0000-000000000012"), Name = "Fan Zone Grill",      Description = "Grilled street food straight from the fan zone.",                                          Address = "Bulevardul Republicii 5, Timișoara",        BusinessTypeId = foodTruck,  ImageUrl = "https://loremflickr.com/640/400/football,grill/all?lock=212", Latitude = 45.7472, Longitude = 21.2152 },
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
                continue;
            }

            if (IsStalePlaceholderImage(existing.ImageUrl))
            {
                existing.ImageUrl = seed.ImageUrl;
            }

            // Backfill only — Latitude/Longitude are new columns, so a database seeded before this
            // feature shipped has them null. Don't clobber an admin-set location once it's there.
            if (existing.Latitude is null && existing.Longitude is null)
            {
                existing.Latitude = seed.Latitude;
                existing.Longitude = seed.Longitude;
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
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000001"), BusinessId = b1,  PackageTypeId = surpriseBag, Name = "Golden Boot Surprise Bag",     Description = "A top-scoring surprise selection of today's leftover dishes.",              Price = 12.99m, Quantity = 5,  WeightKg = 1.5m, PickupStart = At(17,  0), PickupEnd = At(20,  0), ImageUrl = "https://loremflickr.com/640/360/football,goldenboot/all?lock=301" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000002"), BusinessId = b1,  PackageTypeId = mealBox,     Name = "Final Whistle Meal Box",       Description = "A full meal box with a main course and side dish, boxed at full-time.",     Price =  9.99m, Quantity = 3,  WeightKg = 1.2m, PickupStart = At(17,  0), PickupEnd = At(20,  0), ImageUrl = "https://loremflickr.com/640/360/football,whistle/all?lock=302" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000003"), BusinessId = b2,  PackageTypeId = surpriseBag, Name = "Offside Surprise Bag",         Description = "Leftover plates that were flagged for the bench — still perfectly good.",  Price =  8.99m, Quantity = 4,  WeightKg = 1.5m, PickupStart = At(18,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,offside/all?lock=303" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000004"), BusinessId = b2,  PackageTypeId = mealBox,     Name = "Extra Time Meal Box",          Description = "A hearty meal for the ones who always stay till the final minute.",        Price = 10.50m, Quantity = 5,  WeightKg = 1.2m, PickupStart = At(19,  0), PickupEnd = At(22,  0), ImageUrl = "https://loremflickr.com/640/360/football,overtime/all?lock=304" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000005"), BusinessId = b3,  PackageTypeId = surpriseBag, Name = "Derby Day Surprise Bag",       Description = "A mystery mix of leftover homemade Romanian dishes, derby-day style.",     Price =  8.50m, Quantity = 4,  WeightKg = 1.5m, PickupStart = At(19,  0), PickupEnd = At(21, 30), ImageUrl = "https://loremflickr.com/640/360/football,derby/all?lock=305" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000006"), BusinessId = b3,  PackageTypeId = mealBox,     Name = "Hat-Trick Meal Box",           Description = "Soup, main, and dessert — a three-course hat-trick in one box.",           Price = 11.50m, Quantity = 6,  WeightKg = 1.2m, PickupStart = At(17, 30), PickupEnd = At(20, 30), ImageUrl = "https://loremflickr.com/640/360/football,hattrick/all?lock=306" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000007"), BusinessId = b4,  PackageTypeId = breadBag,    Name = "Golden Goal Bread Bag",        Description = "Assorted fresh breads and pastries from the day, straight off the bench.", Price =  6.50m, Quantity = 10, WeightKg = 1.4m, PickupStart = At(16,  0), PickupEnd = At(19,  0), ImageUrl = "https://loremflickr.com/640/360/football,goal/all?lock=307" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000008"), BusinessId = b4,  PackageTypeId = pastryBox,   Name = "Trophy Pastry Box",            Description = "Six championship-worthy pastries: croissants, cinnamon rolls, and more.",  Price =  8.00m, Quantity = 8,  WeightKg = 0.6m, PickupStart = At(15,  0), PickupEnd = At(18,  0), ImageUrl = "https://loremflickr.com/640/360/football,trophy/all?lock=308" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000009"), BusinessId = b5,  PackageTypeId = breadBag,    Name = "Kick-Off Bread Bag",           Description = "End-of-day selection of sourdough, rye, and whole wheat loaves.",          Price =  5.00m, Quantity = 12, WeightKg = 1.4m, PickupStart = At(16, 30), PickupEnd = At(19, 30), ImageUrl = "https://loremflickr.com/640/360/soccer,kickoff/all?lock=309" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000010"), BusinessId = b5,  PackageTypeId = pastryBox,   Name = "Penalty Pastry Box",           Description = "A last-minute lineup of leftover match-day pretzels and sweet rolls.",     Price =  7.50m, Quantity = 8,  WeightKg = 0.6m, PickupStart = At(15, 30), PickupEnd = At(18, 30), ImageUrl = "https://loremflickr.com/640/360/football,penalty/all?lock=310" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000011"), BusinessId = b6,  PackageTypeId = breadBag,    Name = "Corner Kick Bread Bag",        Description = "Fresh focaccia and bread ends, curved in fresh like a corner kick.",       Price =  5.50m, Quantity = 10, WeightKg = 1.4m, PickupStart = At(16,  0), PickupEnd = At(19,  0), ImageUrl = "https://loremflickr.com/640/360/football,corner/all?lock=311" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000012"), BusinessId = b6,  PackageTypeId = pastryBox,   Name = "Champions Pastry Box",         Description = "A finalist's assortment of the day's best leftover pastries.",             Price =  8.50m, Quantity = 6,  WeightKg = 0.6m, PickupStart = At(15,  0), PickupEnd = At(18,  0), ImageUrl = "https://loremflickr.com/640/360/football,champions/all?lock=312" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000013"), BusinessId = b7,  PackageTypeId = surpriseBag, Name = "Half-Time Surprise Bag",       Description = "Leftover sandwiches, muffins, and snacks from the cafe.",                  Price =  7.99m, Quantity = 4,  WeightKg = 1.5m, PickupStart = At(18,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,halftime/all?lock=313" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000014"), BusinessId = b7,  PackageTypeId = pastryBox,   Name = "Stoppage Time Pastry Box",     Description = "The last few pastries added on before the counter closes for the day.",    Price =  6.99m, Quantity = 5,  WeightKg = 0.6m, PickupStart = At(19,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,stoppagetime/all?lock=314" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000015"), BusinessId = b8,  PackageTypeId = surpriseBag, Name = "Yellow Card Surprise Bag",     Description = "Leftover cakes, cookies, and quiches of the day — a caution against waste.",Price =  6.99m, Quantity = 5,  WeightKg = 1.5m, PickupStart = At(17,  0), PickupEnd = At(19,  0), ImageUrl = "https://loremflickr.com/640/360/football,yellowcard/all?lock=315" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000016"), BusinessId = b8,  PackageTypeId = pastryBox,   Name = "Red Card Pastry Box",          Description = "Dark chocolate and espresso pastries, sent off before they go stale.",     Price =  7.25m, Quantity = 5,  WeightKg = 0.6m, PickupStart = At(17, 30), PickupEnd = At(19, 30), ImageUrl = "https://loremflickr.com/640/360/football,redcard/all?lock=316" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000017"), BusinessId = b9,  PackageTypeId = veggieBox,   Name = "Offside Veggie Box",           Description = "Seasonal vegetables and fruit nearing best-before — still match-fit.",     Price =  5.50m, Quantity = 15, WeightKg = 2.0m, PickupStart = At(16,  0), PickupEnd = At(20,  0), ImageUrl = "https://loremflickr.com/640/360/vegetables,market/all?lock=317" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000018"), BusinessId = b9,  PackageTypeId = veggieBox,   Name = "Injury Time Veggie Box",       Description = "A last-minute rescue of fresh produce before it's subbed off the shelf.",  Price =  5.00m, Quantity = 12, WeightKg = 2.0m, PickupStart = At(19,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/vegetables,fresh/all?lock=318" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000019"), BusinessId = b10, PackageTypeId = veggieBox,   Name = "Penalty Box Veggie Box",       Description = "Surplus greens and fruit saved right from the penalty box.",              Price =  5.75m, Quantity = 10, WeightKg = 2.0m, PickupStart = At(16, 30), PickupEnd = At(19, 30), ImageUrl = "https://loremflickr.com/640/360/football,penaltybox/all?lock=319" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000020"), BusinessId = b10, PackageTypeId = mealBox,     Name = "Added Time Meal Box",          Description = "A ready-made meal box rescued in the final added minutes of the day.",     Price =  9.50m, Quantity = 6,  WeightKg = 1.2m, PickupStart = At(18,  0), PickupEnd = At(20, 30), ImageUrl = "https://loremflickr.com/640/360/football,addedtime/all?lock=320" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000021"), BusinessId = b11, PackageTypeId = mealBox,     Name = "World Cup Meal Box",           Description = "A full street-food meal inspired by host nations — tacos, gyros, or noodles.",Price = 10.00m, Quantity = 7,  WeightKg = 1.2m, PickupStart = At(18, 30), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/worldcup,streetfood/all?lock=321" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000022"), BusinessId = b11, PackageTypeId = surpriseBag, Name = "Fan Zone Surprise Bag",        Description = "Mixed snacks, sides, and small bites leftover from the day's service.",    Price =  5.99m, Quantity = 6,  WeightKg = 1.5m, PickupStart = At(20,  0), PickupEnd = At(22,  0), ImageUrl = "https://loremflickr.com/640/360/football,fanzone/all?lock=322" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000023"), BusinessId = b12, PackageTypeId = mealBox,     Name = "Top Scorer Meal Box",          Description = "The crowd favourite: a grilled meal box that scores every time.",          Price = 10.50m, Quantity = 6,  WeightKg = 1.2m, PickupStart = At(18,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,grill/all?lock=323" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000024"), BusinessId = b12, PackageTypeId = surpriseBag, Name = "Stadium Snack Surprise Bag",   Description = "A grab-bag of grilled snacks and sides fresh from the fan zone grill.",    Price =  6.25m, Quantity = 8,  WeightKg = 1.5m, PickupStart = At(19,  0), PickupEnd = At(21, 30), ImageUrl = "https://loremflickr.com/640/360/football,snacks/all?lock=324" }
        };

        // Plausible default tags per package type, so the feature has real demo data out of the box.
        var defaultTagsByType = new Dictionary<Guid, string[]>
        {
            [breadBag] = [DietaryTags.Vegetarian, DietaryTags.ContainsGluten],
            [pastryBox] = [DietaryTags.Vegetarian, DietaryTags.ContainsGluten, DietaryTags.ContainsDairy],
            [veggieBox] = [DietaryTags.Vegan, DietaryTags.Vegetarian, DietaryTags.GlutenFree, DietaryTags.DairyFree],
            [mealBox] = [DietaryTags.ContainsGluten, DietaryTags.ContainsDairy],
        };
        foreach (var seed in seedPackages)
            seed.DietaryTags = [..defaultTagsByType.GetValueOrDefault(seed.PackageTypeId, [])];

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

            // Once a template owns this package, PackageTemplateGenerationService is responsible
            // for its future instances — let this one expire naturally instead of fighting it.
            if (existing.PickupEnd < DateTime.UtcNow && existing.TemplateId is null)
            {
                existing.PickupStart = seed.PickupStart;
                existing.PickupEnd = seed.PickupEnd;
            }

            if (IsStalePlaceholderImage(existing.ImageUrl))
            {
                existing.ImageUrl = seed.ImageUrl;
            }

            // Backfill only — don't clobber a real manager edit once WeightKg is set.
            if (existing.WeightKg <= 0)
            {
                existing.WeightKg = seed.WeightKg;
            }

            // Same backfill-only rule as WeightKg above — can't distinguish "never set" from
            // "manager cleared it", so default to the demo tags either way.
            if (existing.DietaryTags.Count == 0)
            {
                existing.DietaryTags = seed.DietaryTags;
            }
        }

        await db.SaveChangesAsync();
    }

    // Turns the demo business's "Golden Boot Surprise Bag" into a recurring template, so a fresh
    // docker compose up already shows the 🔁 Daily badge and something on /packages/templates.
    // Only runs once — the generation service owns this package's future instances after that.
    private static async Task SeedPackageTemplateAsync(EcoMealDbContext db)
    {
        var templateId = new Guid("66666666-0000-0000-0000-000000000001");
        if (await db.PackageTemplates.AnyAsync(t => t.Id == templateId)) return;

        var linkedPackage = await db.Packages.FindAsync(new Guid("55555555-0000-0000-0000-000000000001"));
        if (linkedPackage is null || linkedPackage.TemplateId is not null) return;

        db.PackageTemplates.Add(new PackageTemplate
        {
            Id = templateId,
            BusinessId = linkedPackage.BusinessId,
            PackageTypeId = linkedPackage.PackageTypeId,
            Name = linkedPackage.Name,
            Description = linkedPackage.Description,
            Price = linkedPackage.Price,
            Quantity = linkedPackage.Quantity,
            WeightKg = linkedPackage.WeightKg,
            DietaryTags = [..linkedPackage.DietaryTags],
            PickupStartTimeUtc = linkedPackage.PickupStart.TimeOfDay,
            PickupEndTimeUtc = linkedPackage.PickupEnd.TimeOfDay,
            ImageUrl = linkedPackage.ImageUrl,
            LastGeneratedDate = DateOnly.FromDateTime(linkedPackage.PickupStart),
        });
        linkedPackage.TemplateId = templateId;

        await db.SaveChangesAsync();
    }

    // Gives the demo customer/manager accounts a lived-in history — orders in every status,
    // spread across the last 14 days so the dashboard trend chart and CSV export have something
    // to show, plus favorites/reviews/notifications. Guarded by "no orders exist yet" so it only
    // ever runs once, on a genuinely fresh database — it never touches real orders placed later.
    private static async Task SeedDemoActivityAsync(EcoMealDbContext db, ApplicationUser demoCustomer, string demoManagerId)
    {
        if (await db.Orders.AnyAsync()) return;

        var managedBusiness = await db.Businesses.FindAsync(DemoManagedBusinessId);
        if (managedBusiness is not null && managedBusiness.ManagerId is null)
            managedBusiness.ManagerId = demoManagerId;

        var b1 = DemoManagedBusinessId;                                    // Stadionul de Gusturi (managed)
        var b2 = new Guid("44444444-0000-0000-0000-000000000002");         // VAR Bistro
        var b3 = new Guid("44444444-0000-0000-0000-000000000003");         // Derby Deli
        var b7 = new Guid("44444444-0000-0000-0000-000000000007");         // Extra Time Café
        var b9 = new Guid("44444444-0000-0000-0000-000000000009");         // Fault Fresh Market
        var b10 = new Guid("44444444-0000-0000-0000-000000000010");        // Penalty Pantry

        var statusIdByName = await db.Statuses.ToDictionaryAsync(s => s.Name, s => s.Id);
        var packagesById = await db.Packages.ToDictionaryAsync(p => p.Id);
        var now = DateTime.UtcNow;

        Order MakeOrder(Guid businessId, Guid packageId, int quantity, string statusName, DateTime createdAt)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = demoCustomer.Id,
                User = demoCustomer,
                BusinessId = businessId,
                StatusId = statusIdByName[statusName],
                CreatedAt = createdAt,
            };
            order.OrderPackages.Add(new OrderPackage { Id = Guid.NewGuid(), OrderId = order.Id, PackageId = packageId, Quantity = quantity });

            // Mirrors OrderService.ApplyStatusChangeAsync: only Confirmed/Completed reserve stock.
            if (statusName is OrderStatuses.Confirmed or OrderStatuses.Completed)
                packagesById[packageId].Quantity -= quantity;

            return order;
        }

        var oldCompleted = MakeOrder(b1, new Guid("55555555-0000-0000-0000-000000000002"), 1, OrderStatuses.Completed, now.AddDays(-12));
        var midCompleted = MakeOrder(b3, new Guid("55555555-0000-0000-0000-000000000005"), 1, OrderStatuses.Completed, now.AddDays(-9));
        var recentCompleted = MakeOrder(b1, new Guid("55555555-0000-0000-0000-000000000001"), 1, OrderStatuses.Completed, now.AddDays(-6));
        var cancelled = MakeOrder(b9, new Guid("55555555-0000-0000-0000-000000000017"), 1, OrderStatuses.Cancelled, now.AddDays(-3));
        var noShow = MakeOrder(b3, new Guid("55555555-0000-0000-0000-000000000006"), 1, OrderStatuses.NoShow, now.AddDays(-4));
        var confirmed = MakeOrder(b2, new Guid("55555555-0000-0000-0000-000000000004"), 1, OrderStatuses.Confirmed, now.AddDays(-1));
        var pending = MakeOrder(b1, new Guid("55555555-0000-0000-0000-000000000008"), 1, OrderStatuses.Pending, now.AddMinutes(-20));

        db.Orders.AddRange(oldCompleted, midCompleted, recentCompleted, cancelled, noShow, confirmed, pending);

        db.Favorites.AddRange(
            new Favorite { Id = Guid.NewGuid(), UserId = demoCustomer.Id, BusinessId = b1, CreatedAt = now },
            new Favorite { Id = Guid.NewGuid(), UserId = demoCustomer.Id, BusinessId = b7, CreatedAt = now },
            new Favorite { Id = Guid.NewGuid(), UserId = demoCustomer.Id, BusinessId = b10, CreatedAt = now }
        );

        db.Reviews.AddRange(
            new Review { Id = Guid.NewGuid(), BusinessId = b1, UserId = demoCustomer.Id, Rating = 5, Comment = "Great surprise bag, saved us from cooking twice!", CreatedAt = now.AddDays(-6) },
            new Review { Id = Guid.NewGuid(), BusinessId = b3, UserId = demoCustomer.Id, Rating = 4, Comment = "Solid portion, will order again.", CreatedAt = now.AddDays(-9) }
        );

        // Orders need to be saved first so the order_numbers sequence assigns OrderNumber before
        // it's referenced in the notification text below.
        await db.SaveChangesAsync();

        db.Notifications.AddRange(
            new Notification { Id = Guid.NewGuid(), UserId = demoCustomer.Id, Message = $"Order #{recentCompleted.OrderNumber:000} at Stadionul de Gusturi is complete — thanks for rescuing food!", Url = "/orders", IsRead = true, CreatedAt = now.AddDays(-6) },
            new Notification { Id = Guid.NewGuid(), UserId = demoCustomer.Id, Message = $"Order #{cancelled.OrderNumber:000} at Fault Fresh Market was cancelled.", Url = "/orders", IsRead = true, CreatedAt = now.AddDays(-3) },
            new Notification { Id = Guid.NewGuid(), UserId = demoCustomer.Id, Message = $"Order #{noShow.OrderNumber:000} at Derby Deli was marked as a no-show — the pickup window closed without it being picked up.", Url = "/orders", IsRead = true, CreatedAt = now.AddDays(-4) },
            new Notification { Id = Guid.NewGuid(), UserId = demoCustomer.Id, Message = $"Order #{confirmed.OrderNumber:000} at VAR Bistro was confirmed — show your QR code at pickup.", Url = $"/orders/pickup/{confirmed.Id}", IsRead = false, CreatedAt = now.AddDays(-1) },
            new Notification { Id = Guid.NewGuid(), UserId = demoManagerId, Message = $"New order #{pending.OrderNumber:000} from {demoCustomer.Name} at Stadionul de Gusturi needs confirmation.", Url = "/orders/manage", IsRead = false, CreatedAt = now.AddMinutes(-20) }
        );

        await db.SaveChangesAsync();
    }

    // True when there's no image, or it's on a placeholder host we've retired (picsum.photos) —
    // not loremflickr.com, which is also the current seed default and could be an admin's own choice.
    private static bool IsStalePlaceholderImage(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) || imageUrl.Contains("picsum.photos");
}