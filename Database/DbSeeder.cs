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
    // Two more customer accounts purely to give the Phase 11 /impact leaderboard more than one
    // row on a fresh database — one opted in, one deliberately opted OUT despite also having a
    // Completed order, so the "opt-in, not just 'has orders'" privacy filter is visibly working
    // rather than just true in theory. See SeedLeaderboardDemoDataAsync.
    private const string DemoCustomerEmail2 = "demo.customer2@ecomeal.local";
    private const string DemoCustomerPassword2 = "Demo123!";
    private const string DemoCustomerEmail3 = "demo.customer3@ecomeal.local";
    private const string DemoCustomerPassword3 = "Demo123!";
    private const string DemoManagerEmail = "demo.manager@ecomeal.local";
    private const string DemoManagerPassword = "Demo123!";
    // A second demo manager so a fresh database already demonstrates multiple staff per
    // business (this one shares Stadionul de Gusturi with the first demo manager below).
    private const string DemoManagerEmail2 = "demo.manager2@ecomeal.local";
    private const string DemoManagerPassword2 = "Demo123!";

    // The managed business for the demo manager/orders below — Stadionul de Gusturi.
    private static readonly Guid DemoManagedBusinessId = new("44444444-0000-0000-0000-000000000001");
    // The demo manager's second business — VAR Bistro — so the business switcher has something
    // to switch between out of the box (demonstrates one staffer, multiple businesses).
    private static readonly Guid DemoSecondBusinessId = new("44444444-0000-0000-0000-000000000002");

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

        ApplicationUser? adminUser = null;
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("SeedAdmin:Email/SeedAdmin:Password are not configured — no admin account will be seeded.");
        }
        else
        {
            adminUser = await GetOrCreateUserAsync(userManager, adminEmail, "Admin", AppRoles.Admin, adminPassword, logger);
        }

        var demoCustomer = await GetOrCreateUserAsync(userManager, DemoCustomerEmail, "Demo Customer", AppRoles.Customer, DemoCustomerPassword, logger);
        var demoCustomer2 = await GetOrCreateUserAsync(userManager, DemoCustomerEmail2, "Demo Customer Two", AppRoles.Customer, DemoCustomerPassword2, logger);
        var demoCustomer3 = await GetOrCreateUserAsync(userManager, DemoCustomerEmail3, "Demo Customer Three", AppRoles.Customer, DemoCustomerPassword3, logger);
        var demoManager = await GetOrCreateUserAsync(userManager, DemoManagerEmail, "Demo Manager", AppRoles.BusinessManager, DemoManagerPassword, logger);
        var demoManager2 = await GetOrCreateUserAsync(userManager, DemoManagerEmail2, "Demo Manager Two", AppRoles.BusinessManager, DemoManagerPassword2, logger);

        var db = services.GetRequiredService<EcoMealDbContext>();
        await SeedBusinessTypesAsync(db);
        await SeedPackageTypesAsync(db);
        await SeedStatusesAsync(db);
        await SeedBusinessesAsync(db);
        await SeedPackagesAsync(db);
        await SeedPackageTemplateAsync(db);
        await SeedBusinessHoursAsync(db);
        await SeedBusinessClosuresAsync(db);
        await SeedBusinessStaffAsync(db, demoManager?.Id, demoManager2?.Id);

        if (demoCustomer is not null)
            await SeedApprovalDemoBusinessesAsync(db, demoCustomer.Id);

        await SeedModerationDemoDataAsync(db);

        if (demoCustomer is not null && demoManager is not null)
            await SeedDemoActivityAsync(db, demoCustomer, demoManager.Id);

        if (demoCustomer is not null)
        {
            await SeedReportsAndAuditLogAsync(db, demoCustomer, adminUser, demoManager?.Id, demoManager2?.Id);
            await SeedLeaderboardDemoDataAsync(db, demoCustomer, demoCustomer2, demoCustomer3);
        }
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

        // Anchored to "now" instead of a fixed hour, so this one package is always inside
        // Home.razor's one-hour "closing soon" badge/sort window on a fresh seed, whatever time of
        // day the app starts — demos Phase 10's countdown badge without waiting around for it.
        var closingSoonDemoStart = DateTime.UtcNow.AddMinutes(-30);
        var closingSoonDemoEnd = DateTime.UtcNow.AddMinutes(25);

        // Same "anchored to now" trick, at the demo customer's own favorited + previously-ordered
        // business — demos the AI near-expiry nudge's dietary-tag match against the demo
        // customer's earlier "Final Whistle Meal Box" order (same mealBox default tags).
        var nearExpiryNudgeDemoStart = DateTime.UtcNow.AddMinutes(-15);
        var nearExpiryNudgeDemoEnd = DateTime.UtcNow.AddMinutes(20);

        // Same "anchored to now" trick, closing within MarkdownSettings.ClosingWindow (3h) with
        // stock still unsold — demos the /packages markdown-pricing suggestion. Same business/type
        // (mealBox, b1) as hist1/hist3 below, priced above their real 9.50/9.99 sell-through comps.
        var markdownDemoStart = DateTime.UtcNow.AddHours(-2);
        var markdownDemoEnd = DateTime.UtcNow.AddMinutes(90);

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
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000014"), BusinessId = b7,  PackageTypeId = pastryBox,   Name = "Stoppage Time Pastry Box",     Description = "The last few pastries added on before the counter closes for the day.",    Price =  6.99m, Quantity = 5,  WeightKg = 0.6m, PickupStart = closingSoonDemoStart, PickupEnd = closingSoonDemoEnd, ImageUrl = "https://loremflickr.com/640/360/football,stoppagetime/all?lock=314" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000015"), BusinessId = b8,  PackageTypeId = surpriseBag, Name = "Yellow Card Surprise Bag",     Description = "Leftover cakes, cookies, and quiches of the day — a caution against waste.",Price =  6.99m, Quantity = 5,  WeightKg = 1.5m, PickupStart = At(17,  0), PickupEnd = At(19,  0), ImageUrl = "https://loremflickr.com/640/360/football,yellowcard/all?lock=315" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000016"), BusinessId = b8,  PackageTypeId = pastryBox,   Name = "Red Card Pastry Box",          Description = "Dark chocolate and espresso pastries, sent off before they go stale.",     Price =  7.25m, Quantity = 5,  WeightKg = 0.6m, PickupStart = At(17, 30), PickupEnd = At(19, 30), ImageUrl = "https://loremflickr.com/640/360/football,redcard/all?lock=316" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000017"), BusinessId = b9,  PackageTypeId = veggieBox,   Name = "Offside Veggie Box",           Description = "Seasonal vegetables and fruit nearing best-before — still match-fit.",     Price =  5.50m, Quantity = 15, WeightKg = 2.0m, PickupStart = At(16,  0), PickupEnd = At(20,  0), ImageUrl = "https://loremflickr.com/640/360/vegetables,market/all?lock=317" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000018"), BusinessId = b9,  PackageTypeId = veggieBox,   Name = "Injury Time Veggie Box",       Description = "A last-minute rescue of fresh produce before it's subbed off the shelf.",  Price =  5.00m, Quantity = 12, WeightKg = 2.0m, PickupStart = At(19,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/vegetables,fresh/all?lock=318" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000019"), BusinessId = b10, PackageTypeId = veggieBox,   Name = "Penalty Box Veggie Box",       Description = "Surplus greens and fruit saved right from the penalty box.",              Price =  5.75m, Quantity = 10, WeightKg = 2.0m, PickupStart = At(16, 30), PickupEnd = At(19, 30), ImageUrl = "https://loremflickr.com/640/360/football,penaltybox/all?lock=319" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000020"), BusinessId = b10, PackageTypeId = mealBox,     Name = "Added Time Meal Box",          Description = "A ready-made meal box rescued in the final added minutes of the day.",     Price =  9.50m, Quantity = 6,  WeightKg = 1.2m, PickupStart = At(18,  0), PickupEnd = At(20, 30), ImageUrl = "https://loremflickr.com/640/360/football,addedtime/all?lock=320" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000021"), BusinessId = b11, PackageTypeId = mealBox,     Name = "World Cup Meal Box",           Description = "A full street-food meal inspired by host nations — tacos, gyros, or noodles.",Price = 10.00m, Quantity = 7,  WeightKg = 1.2m, PickupStart = At(18, 30), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/worldcup,streetfood/all?lock=321" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000022"), BusinessId = b11, PackageTypeId = surpriseBag, Name = "Fan Zone Surprise Bag",        Description = "Mixed snacks, sides, and small bites leftover from the day's service.",    Price =  5.99m, Quantity = 6,  WeightKg = 1.5m, PickupStart = At(20,  0), PickupEnd = At(22,  0), ImageUrl = "https://loremflickr.com/640/360/football,fanzone/all?lock=322" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000023"), BusinessId = b12, PackageTypeId = mealBox,     Name = "Top Scorer Meal Box",          Description = "The crowd favourite: a grilled meal box that scores every time.",          Price = 10.50m, Quantity = 6,  WeightKg = 1.2m, PickupStart = At(18,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,grill/all?lock=323" },
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000024"), BusinessId = b12, PackageTypeId = surpriseBag, Name = "Stadium Snack Surprise Bag",   Description = "A grab-bag of grilled snacks and sides fresh from the fan zone grill.",    Price =  6.25m, Quantity = 8,  WeightKg = 1.5m, PickupStart = At(19,  0), PickupEnd = At(21, 30), ImageUrl = "https://loremflickr.com/640/360/football,snacks/all?lock=324" },
            // Phase 12 demo: deliberately Quantity = 1 so confirming a single order against it is
            // enough to watch "1 left" flip to "Sold out" live on another open BusinessDetail.razor
            // tab (PackageStockBroadcaster) without either tab refreshing.
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000025"), BusinessId = b1,  PackageTypeId = mealBox,     Name = "Last One Standing Box",        Description = "Down to the final portion of the day — first to confirm gets it.",         Price =  9.25m, Quantity = 1,  WeightKg = 1.2m, PickupStart = At(17,  0), PickupEnd = At(21,  0), ImageUrl = "https://loremflickr.com/640/360/football,lastminute/all?lock=325" },
            // Phase 3 demo: closing soon, stock unclaimed, at a business the demo customer both
            // favorites and has a Completed order from — see nearExpiryNudgeDemoStart/End above.
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000026"), BusinessId = b1,  PackageTypeId = mealBox,     Name = "Late Save Meal Box",           Description = "One more meal box rescued in the closing minutes before the counter shuts.",Price =  9.75m, Quantity = 2,  WeightKg = 1.2m, PickupStart = nearExpiryNudgeDemoStart, PickupEnd = nearExpiryNudgeDemoEnd, ImageUrl = "https://loremflickr.com/640/360/football,latesave/all?lock=326" },
            // Phase 5 demo: see markdownDemoStart/End above — priced above this business's own
            // recent mealBox comps, still fully unsold with its window closing soon.
            new Package { Id = new Guid("55555555-0000-0000-0000-000000000027"), BusinessId = b1,  PackageTypeId = mealBox,     Name = "Away Day Meal Box",            Description = "A road-trip-sized meal box, priced for away-day appetites.",               Price = 12.99m, Quantity = 4,  WeightKg = 1.2m, PickupStart = markdownDemoStart, PickupEnd = markdownDemoEnd, ImageUrl = "https://loremflickr.com/640/360/football,awayday/all?lock=327" }
        };

        // Plausible default tags per package type, so the feature has real demo data out of the box.
        var defaultTagsByType = new Dictionary<Guid, string[]>
        {
            [breadBag] = [DietaryTags.Vegetarian, DietaryTags.ContainsGluten],
            [pastryBox] = [DietaryTags.Vegetarian, DietaryTags.ContainsGluten, DietaryTags.ContainsDairy],
            [veggieBox] = [DietaryTags.Vegan, DietaryTags.Vegetarian, DietaryTags.GlutenFree, DietaryTags.DairyFree],
            [mealBox] = [DietaryTags.ContainsGluten, DietaryTags.ContainsDairy],
        };
        // Per-package overrides so every tag in Constants.DietaryTags.All — including Halal and
        // Contains Nuts, which no package-type default covers — has at least one real package to
        // filter to on the Phase 10 home page filter.
        var tagOverridesByPackage = new Dictionary<Guid, string[]>
        {
            [new Guid("55555555-0000-0000-0000-000000000021")] = [DietaryTags.Halal, DietaryTags.ContainsGluten, DietaryTags.ContainsDairy],
            [new Guid("55555555-0000-0000-0000-000000000008")] = [DietaryTags.Vegetarian, DietaryTags.ContainsGluten, DietaryTags.ContainsDairy, DietaryTags.ContainsNuts],
        };
        foreach (var seed in seedPackages)
            seed.DietaryTags = tagOverridesByPackage.TryGetValue(seed.Id, out var overrideTags)
                ? [..overrideTags]
                : [..defaultTagsByType.GetValueOrDefault(seed.PackageTypeId, [])];

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
                // A refreshed window re-enters "closing soon"/markdown territory (see the
                // *DemoStart vars above) — let NearExpiryNudgeService and the /packages badge
                // reconsider it instead of treating a previous run's nudge/dismissal as current.
                existing.NearExpiryNudgeSentAt = null;
                existing.MarkdownDismissedAt = null;
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

    // A plausible weekly schedule per business type, so the "closed now" indicator has real
    // variety on a fresh database instead of every kitchen reading the same open/closed.
    private static async Task SeedBusinessHoursAsync(EcoMealDbContext db)
    {
        if (await db.BusinessHours.AnyAsync()) return;

        List<BusinessHours> Week(Guid businessId, TimeOnly open, TimeOnly close, DayOfWeek? closedDay = null) =>
            Enum.GetValues<DayOfWeek>().Select(day =>
            {
                var isClosed = day == closedDay;
                return new BusinessHours
                {
                    Id = Guid.NewGuid(), BusinessId = businessId, DayOfWeek = day, IsClosed = isClosed,
                    OpenTime = isClosed ? null : open, CloseTime = isClosed ? null : close,
                };
            }).ToList();

        var hours = new List<BusinessHours>();
        // Restaurants — evening service, closed one weekday each (a common industry pattern).
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000001"), new TimeOnly(12, 0), new TimeOnly(23, 0), DayOfWeek.Monday));
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000002"), new TimeOnly(12, 0), new TimeOnly(22, 30), DayOfWeek.Monday));
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000003"), new TimeOnly(11, 30), new TimeOnly(22, 0), DayOfWeek.Tuesday));
        // Bakeries — early morning through early evening.
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000004"), new TimeOnly(7, 0), new TimeOnly(19, 0)));
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000005"), new TimeOnly(6, 30), new TimeOnly(18, 30)));
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000006"), new TimeOnly(7, 0), new TimeOnly(18, 0), DayOfWeek.Sunday));
        // Cafes — mid-morning through evening.
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000007"), new TimeOnly(8, 0), new TimeOnly(21, 0)));
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000008"), new TimeOnly(8, 0), new TimeOnly(20, 0)));
        // Grocery stores — long hours, open every day.
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000009"), new TimeOnly(8, 0), new TimeOnly(22, 0)));
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000010"), new TimeOnly(9, 0), new TimeOnly(21, 0)));
        // Food trucks — evening only.
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000011"), new TimeOnly(17, 0), new TimeOnly(23, 0)));
        hours.AddRange(Week(new Guid("44444444-0000-0000-0000-000000000012"), new TimeOnly(17, 0), new TimeOnly(22, 30)));

        db.BusinessHours.AddRange(hours);
        await db.SaveChangesAsync();
    }

    // One business closed right now (shows the holiday-closure banner immediately) and one
    // closed starting in a few weeks (shows the closures list without affecting "closed now" yet).
    private static async Task SeedBusinessClosuresAsync(EcoMealDbContext db)
    {
        var activeClosureBusinessId = new Guid("44444444-0000-0000-0000-000000000008");
        var upcomingClosureBusinessId = new Guid("44444444-0000-0000-0000-000000000004");

        if (await db.BusinessClosures.AnyAsync(c => c.BusinessId == activeClosureBusinessId || c.BusinessId == upcomingClosureBusinessId))
            return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        db.BusinessClosures.AddRange(
            new BusinessClosure { Id = Guid.NewGuid(), BusinessId = activeClosureBusinessId, StartDate = today.AddDays(-1), EndDate = today.AddDays(2), Reason = "Staff on holiday — back Wednesday." },
            new BusinessClosure { Id = Guid.NewGuid(), BusinessId = upcomingClosureBusinessId, StartDate = today.AddDays(20), EndDate = today.AddDays(27), Reason = "Annual summer closure." }
        );

        await db.SaveChangesAsync();
    }

    // Staffs the two demo managers across the demo businesses, demonstrating both directions of the
    // many-to-many: demoManagerId works Stadionul de Gusturi AND VAR Bistro (one staffer, several
    // businesses), while demoManager2Id joins them at Stadionul de Gusturi (several staff, one
    // business). Idempotent by (BusinessId, UserId) pair, same reconcile-don't-clobber style as the rest.
    private static async Task SeedBusinessStaffAsync(EcoMealDbContext db, string? demoManagerId, string? demoManager2Id)
    {
        var pairs = new List<(Guid BusinessId, string UserId)>();
        if (demoManagerId is not null)
        {
            pairs.Add((DemoManagedBusinessId, demoManagerId));
            pairs.Add((DemoSecondBusinessId, demoManagerId));
        }
        if (demoManager2Id is not null)
            pairs.Add((DemoManagedBusinessId, demoManager2Id));

        if (pairs.Count == 0) return;

        var businessIds = pairs.Select(p => p.BusinessId).Distinct().ToList();
        var existing = await db.BusinessStaff
            .Where(s => businessIds.Contains(s.BusinessId))
            .Select(s => new { s.BusinessId, s.UserId })
            .ToListAsync();

        var now = DateTime.UtcNow;
        var added = false;
        foreach (var (businessId, userId) in pairs)
        {
            if (existing.Any(e => e.BusinessId == businessId && e.UserId == userId))
                continue;

            db.BusinessStaff.Add(new BusinessStaff { Id = Guid.NewGuid(), BusinessId = businessId, UserId = userId, AssignedAt = now });
            added = true;
        }

        if (added)
            await db.SaveChangesAsync();
    }

    // Demonstrates the Phase 9 self-service application flow — a pending application and a
    // rejected one so the admin's approval queue (Businesses.razor) isn't empty on a fresh DB.
    private static async Task SeedApprovalDemoBusinessesAsync(EcoMealDbContext db, string demoCustomerId)
    {
        var pendingId = new Guid("44444444-0000-0000-0000-000000000013");
        var rejectedId = new Guid("44444444-0000-0000-0000-000000000014");
        if (await db.Businesses.AnyAsync(b => b.Id == pendingId || b.Id == rejectedId)) return;

        var restaurant = new Guid("11111111-0000-0000-0000-000000000001");
        var foodTruck  = new Guid("11111111-0000-0000-0000-000000000005");

        db.Businesses.AddRange(
            new Business
            {
                Id = pendingId, Name = "Golazo Grill",
                Description = "A second street-food truck applying to join the lineup.",
                Address = "Strada Torontalului 12, Timișoara", BusinessTypeId = foodTruck,
                Status = BusinessStatuses.PendingApproval, SubmittedByUserId = demoCustomerId,
            },
            new Business
            {
                Id = rejectedId, Name = "Offside Kitchen",
                Description = "A restaurant application that couldn't be verified.",
                Address = "Unverified address, Timișoara", BusinessTypeId = restaurant,
                Status = BusinessStatuses.Rejected, SubmittedByUserId = demoCustomerId,
                RejectionReason = "Address could not be verified — please resubmit with a valid street address.",
            });

        await db.SaveChangesAsync();
    }

    // Marks one existing business and one existing package as moderated, so /businesses and
    // /packages already show a Hidden badge and an Unhide action on a fresh DB. Backfill-only —
    // never re-hides something an admin has since unhidden.
    private static async Task SeedModerationDemoDataAsync(EcoMealDbContext db)
    {
        var fanZoneGrill = await db.Businesses.FindAsync(new Guid("44444444-0000-0000-0000-000000000012"));
        if (fanZoneGrill is not null && !fanZoneGrill.IsHidden && fanZoneGrill.HiddenReason is null)
        {
            fanZoneGrill.IsHidden = true;
            fanZoneGrill.HiddenReason = "Awaiting an updated food safety certificate.";
        }

        var redCardPastryBox = await db.Packages.FindAsync(new Guid("55555555-0000-0000-0000-000000000016"));
        if (redCardPastryBox is not null && !redCardPastryBox.IsHidden && redCardPastryBox.HiddenReason is null)
        {
            redCardPastryBox.IsHidden = true;
            redCardPastryBox.HiddenReason = "Reported for inaccurate allergen labeling — under review.";
        }

        await db.SaveChangesAsync();
    }

    // Populates /reports and /audit-log with a history consistent with everything else this file
    // seeds (the staff assignments, the rejected application, the hidden business/package above).
    // Guarded on Reports so it only ever runs once, same as SeedDemoActivityAsync below.
    private static async Task SeedReportsAndAuditLogAsync(EcoMealDbContext db, ApplicationUser demoCustomer, ApplicationUser? adminUser, string? demoManagerId, string? demoManager2Id)
    {
        if (await db.Reports.AnyAsync()) return;

        var now = DateTime.UtcNow;
        var actorId = adminUser?.Id ?? demoCustomer.Id;
        var actorName = adminUser?.Name ?? "Admin";

        var fanZoneGrillId = new Guid("44444444-0000-0000-0000-000000000012");
        var foodTruckMundialId = new Guid("44444444-0000-0000-0000-000000000011");
        var redCardPastryBoxId = new Guid("55555555-0000-0000-0000-000000000016");
        var yellowCardSurpriseBagId = new Guid("55555555-0000-0000-0000-000000000015");
        var stadionulId = DemoManagedBusinessId;
        var varBistroId = DemoSecondBusinessId;
        var golazoGrillId = new Guid("44444444-0000-0000-0000-000000000013");
        var offsideKitchenId = new Guid("44444444-0000-0000-0000-000000000014");

        const string menuPhotosReason = "Menu photos look reused from another truck.";
        const string nutsReason = "Description doesn't mention it contains nuts.";
        const string priceReason = "Price seems high for the portion size.";
        const string truckLocationReason = "Truck wasn't at the listed pickup location yesterday.";
        const string rejectionReason = "Address could not be verified — please resubmit with a valid street address.";
        const string businessHiddenReason = "Awaiting an updated food safety certificate.";
        const string packageHiddenReason = "Reported for inaccurate allergen labeling — under review.";

        db.Reports.AddRange(
            new Report { Id = Guid.NewGuid(), ReporterUserId = demoCustomer.Id, TargetType = AuditTargetTypes.Business, TargetId = fanZoneGrillId, Reason = menuPhotosReason, Status = ReportStatuses.ActionTaken, CreatedAt = now.AddDays(-2).AddHours(-1), ResolvedAt = now.AddDays(-2), ResolvedByUserId = actorId },
            new Report { Id = Guid.NewGuid(), ReporterUserId = demoCustomer.Id, TargetType = AuditTargetTypes.Package, TargetId = redCardPastryBoxId, Reason = nutsReason, Status = ReportStatuses.ActionTaken, CreatedAt = now.AddDays(-3).AddHours(-1), ResolvedAt = now.AddDays(-3), ResolvedByUserId = actorId },
            new Report { Id = Guid.NewGuid(), ReporterUserId = demoCustomer.Id, TargetType = AuditTargetTypes.Package, TargetId = yellowCardSurpriseBagId, Reason = priceReason, Status = ReportStatuses.Dismissed, CreatedAt = now.AddDays(-1).AddHours(-2), ResolvedAt = now.AddDays(-1), ResolvedByUserId = actorId },
            new Report { Id = Guid.NewGuid(), ReporterUserId = demoCustomer.Id, TargetType = AuditTargetTypes.Business, TargetId = foodTruckMundialId, Reason = truckLocationReason, Status = ReportStatuses.Open, CreatedAt = now.AddHours(-6) }
        );

        var entries = new List<AuditLog>
        {
            Entry(demoCustomer.Id, demoCustomer.Name, AuditActions.BusinessApplied, AuditTargetTypes.Business, golazoGrillId, "Golazo Grill", null, now.AddDays(-6)),
            Entry(demoCustomer.Id, demoCustomer.Name, AuditActions.BusinessApplied, AuditTargetTypes.Business, offsideKitchenId, "Offside Kitchen", null, now.AddDays(-6).AddMinutes(10)),
            Entry(actorId, actorName, AuditActions.BusinessRejected, AuditTargetTypes.Business, offsideKitchenId, "Offside Kitchen", rejectionReason, now.AddDays(-5)),
        };

        if (demoManagerId is not null)
        {
            entries.Add(Entry(actorId, actorName, AuditActions.BusinessStaffAdded, AuditTargetTypes.Business, stadionulId, "Stadionul de Gusturi", "Added Demo Manager as staff", now.AddDays(-5).AddHours(1)));
            entries.Add(Entry(actorId, actorName, AuditActions.BusinessStaffAdded, AuditTargetTypes.Business, varBistroId, "VAR Bistro", "Added Demo Manager as staff", now.AddDays(-5).AddHours(1).AddMinutes(5)));
        }
        if (demoManager2Id is not null)
            entries.Add(Entry(actorId, actorName, AuditActions.BusinessStaffAdded, AuditTargetTypes.Business, stadionulId, "Stadionul de Gusturi", "Added Demo Manager Two as staff", now.AddDays(-5).AddHours(1).AddMinutes(10)));

        entries.Add(Entry(actorId, actorName, AuditActions.PackageHidden, AuditTargetTypes.Package, redCardPastryBoxId, "Red Card Pastry Box", packageHiddenReason, now.AddDays(-3)));
        entries.Add(Entry(actorId, actorName, AuditActions.ReportActionTaken, AuditTargetTypes.Package, redCardPastryBoxId, "Red Card Pastry Box", nutsReason, now.AddDays(-3)));
        entries.Add(Entry(actorId, actorName, AuditActions.BusinessHidden, AuditTargetTypes.Business, fanZoneGrillId, "Fan Zone Grill", businessHiddenReason, now.AddDays(-2)));
        entries.Add(Entry(actorId, actorName, AuditActions.ReportActionTaken, AuditTargetTypes.Business, fanZoneGrillId, "Fan Zone Grill", menuPhotosReason, now.AddDays(-2)));
        entries.Add(Entry(actorId, actorName, AuditActions.ReportDismissed, AuditTargetTypes.Package, yellowCardSurpriseBagId, "Yellow Card Surprise Bag", priceReason, now.AddDays(-1)));

        db.AuditLogs.AddRange(entries);

        await db.SaveChangesAsync();
    }

    private static readonly Guid LeaderboardOrder2AId = new("88888888-0000-0000-0000-000000000001");
    private static readonly Guid LeaderboardOrder2BId = new("88888888-0000-0000-0000-000000000002");
    private static readonly Guid LeaderboardOrder3AId = new("88888888-0000-0000-0000-000000000003");

    // Phase 11: opts the primary demo customer into the /impact leaderboard (always — these are fake
    // demo identities, not real preferences) and adds two more so the board has more than one row.
    // demoCustomer3 also gets a real Completed order but stays opted OUT, so the "opt-in, not just
    // 'has orders'" privacy filter is visibly working on a fresh database, not just true in theory.
    private static async Task SeedLeaderboardDemoDataAsync(EcoMealDbContext db, ApplicationUser demoCustomer, ApplicationUser? demoCustomer2, ApplicationUser? demoCustomer3)
    {
        demoCustomer.ShowOnLeaderboard = true;
        if (demoCustomer2 is not null) demoCustomer2.ShowOnLeaderboard = true;
        if (demoCustomer3 is not null) demoCustomer3.ShowOnLeaderboard = false;
        await db.SaveChangesAsync();

        if (demoCustomer2 is null && demoCustomer3 is null) return;
        if (await db.Orders.AnyAsync(o => o.Id == LeaderboardOrder2AId || o.Id == LeaderboardOrder2BId || o.Id == LeaderboardOrder3AId))
            return;

        var completedStatusId = await db.Statuses.Where(s => s.Name == OrderStatuses.Completed).Select(s => s.Id).FirstAsync();
        var packageIds = new[]
        {
            new Guid("55555555-0000-0000-0000-000000000007"), // Golden Goal Bread Bag
            new Guid("55555555-0000-0000-0000-000000000011"), // Corner Kick Bread Bag
            new Guid("55555555-0000-0000-0000-000000000013"), // Half-Time Surprise Bag
        };
        var packages = await db.Packages.Where(p => packageIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var now = DateTime.UtcNow;

        Order MakeLeaderboardOrder(Guid id, ApplicationUser user, Guid packageId, int quantity, DateTime createdAt)
        {
            var package = packages[packageId];
            var order = new Order
            {
                Id = id, UserId = user.Id, User = user, BusinessId = package.BusinessId,
                StatusId = completedStatusId, CreatedAt = createdAt,
            };
            order.OrderPackages.Add(new OrderPackage { Id = Guid.NewGuid(), OrderId = id, PackageId = packageId, Quantity = quantity });
            // Same "only Confirmed/Completed reserve stock" rule SeedDemoActivityAsync's MakeOrder follows.
            package.Quantity -= quantity;

            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(), OrderId = id, Amount = quantity * package.Price, Currency = "ron",
                StripeCheckoutSessionId = $"cs_demo_{id:N}", StripePaymentIntentId = $"pi_demo_{id:N}",
                Status = PaymentStatuses.Succeeded, CreatedAt = createdAt,
            });
            db.OrderPickupPasses.Add(new OrderPickupPass { Id = Guid.NewGuid(), OrderId = id, Label = "Pickup pass", CreatedAt = createdAt, RedeemedAt = createdAt });

            return order;
        }

        var newOrders = new List<Order>();
        if (demoCustomer2 is not null)
        {
            newOrders.Add(MakeLeaderboardOrder(LeaderboardOrder2AId, demoCustomer2, packageIds[0], 2, now.AddDays(-2)));
            newOrders.Add(MakeLeaderboardOrder(LeaderboardOrder2BId, demoCustomer2, packageIds[1], 3, now.AddDays(-1)));
        }
        if (demoCustomer3 is not null)
            newOrders.Add(MakeLeaderboardOrder(LeaderboardOrder3AId, demoCustomer3, packageIds[2], 2, now.AddDays(-3)));

        db.Orders.AddRange(newOrders);
        await db.SaveChangesAsync();
    }

    private static AuditLog Entry(string actorId, string actorName, string action, string targetType, Guid targetId, string targetName, string? details, DateTime createdAt) =>
        new()
        {
            Id = Guid.NewGuid(), ActorUserId = actorId, ActorName = actorName, Action = action,
            TargetType = targetType, TargetId = targetId.ToString(), TargetName = targetName, Details = details, CreatedAt = createdAt,
        };

    // Gives the demo customer/manager accounts a lived-in history — orders in every status,
    // spread across the last 14 days so the dashboard trend chart and CSV export have something
    // to show, plus favorites/reviews/notifications. Guarded by "no orders exist yet" so it only
    // ever runs once, on a genuinely fresh database — it never touches real orders placed later.
    private static async Task SeedDemoActivityAsync(EcoMealDbContext db, ApplicationUser demoCustomer, string demoManagerId)
    {
        if (await db.Orders.AnyAsync()) return;

        var b1 = DemoManagedBusinessId;
        var b2 = new Guid("44444444-0000-0000-0000-000000000002");
        var b3 = new Guid("44444444-0000-0000-0000-000000000003");
        var b7 = new Guid("44444444-0000-0000-0000-000000000007");
        var b9 = new Guid("44444444-0000-0000-0000-000000000009");
        var b10 = new Guid("44444444-0000-0000-0000-000000000010");

        var statusIdByName = await db.Statuses.ToDictionaryAsync(s => s.Name, s => s.Id);
        var packagesById = await db.Packages.ToDictionaryAsync(p => p.Id);
        var now = DateTime.UtcNow;
        var payments = new List<Payment>();
        var newPackages = new List<Package>();
        var pickupPasses = new List<OrderPickupPass>();

        // Every order now only exists once its Stripe Checkout payment is confirmed (see
        // CheckoutService), so each seeded order gets a matching Payment — refunded for the
        // cancelled one (mirrors OrderService's refund-on-cancel), kept Succeeded for the no-show
        // (that's what makes the no-show fee real).
        Order MakeOrder(Guid businessId, Guid packageId, int quantity, string statusName, DateTime createdAt, bool refunded = false)
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

            // Every order that ever reached Confirmed gets a pickup pass, same as
            // OrderService.ApplyStatusChangeAsync — redeemed at completion time, left open for a
            // no-show, and (for the still-Confirmed order below) split into several afterwards to
            // demo the group-pickup feature.
            if (statusName is OrderStatuses.Confirmed or OrderStatuses.Completed or OrderStatuses.NoShow)
            {
                pickupPasses.Add(new OrderPickupPass
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    Label = "Pickup pass",
                    CreatedAt = createdAt,
                    RedeemedAt = statusName == OrderStatuses.Completed ? createdAt : null,
                });
            }

            payments.Add(new Payment
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Amount = quantity * packagesById[packageId].Price,
                Currency = "ron",
                StripeCheckoutSessionId = $"cs_demo_{order.Id:N}",
                StripePaymentIntentId = $"pi_demo_{order.Id:N}",
                Status = refunded ? PaymentStatuses.Refunded : PaymentStatuses.Succeeded,
                CreatedAt = createdAt,
                RefundedAt = refunded ? createdAt.AddMinutes(5) : null,
            });

            return order;
        }

        var oldCompleted = MakeOrder(b1, new Guid("55555555-0000-0000-0000-000000000002"), 1, OrderStatuses.Completed, now.AddDays(-12));
        var midCompleted = MakeOrder(b3, new Guid("55555555-0000-0000-0000-000000000005"), 1, OrderStatuses.Completed, now.AddDays(-9));
        var recentCompleted = MakeOrder(b1, new Guid("55555555-0000-0000-0000-000000000001"), 1, OrderStatuses.Completed, now.AddDays(-6));
        var cancelled = MakeOrder(b9, new Guid("55555555-0000-0000-0000-000000000017"), 1, OrderStatuses.Cancelled, now.AddDays(-3), refunded: true);
        var noShow = MakeOrder(b3, new Guid("55555555-0000-0000-0000-000000000006"), 1, OrderStatuses.NoShow, now.AddDays(-4));
        var confirmed = MakeOrder(b2, new Guid("55555555-0000-0000-0000-000000000004"), 1, OrderStatuses.Confirmed, now.AddDays(-1));
        var pending = MakeOrder(b1, new Guid("55555555-0000-0000-0000-000000000008"), 1, OrderStatuses.Pending, now.AddMinutes(-20));

        // Demos the Phase 1 group-pickup feature on a fresh database: swap this still-Confirmed
        // order's single default pass for three, same as SplitPickupPassesAsync would.
        pickupPasses.RemoveAll(p => p.OrderId == confirmed.Id);
        pickupPasses.AddRange(new[] { "Pass 1", "Pass 2", "Pass 3" }.Select((label, i) => new OrderPickupPass
        {
            Id = Guid.NewGuid(),
            OrderId = confirmed.Id,
            Label = label,
            // A millisecond apart, not identical — the pickup page sorts tabs by CreatedAt, and a
            // gap under Postgres's timestamptz precision (microseconds) would round away to a tie,
            // which Postgres doesn't break in insertion order.
            CreatedAt = now.AddDays(-1).AddMilliseconds(i + 1),
        }));

        // Backs the Phase 8 analytics card: closed pickup windows, each completed for less than
        // its full quantity, so sell-through lands below 100% and the hourly chart gets more than
        // one bar. Unlike the packages above, these are already past their window, so they never
        // show up as live/orderable — only in order history and dashboard analytics.
        Package HistoricalPackage(Guid id, Guid businessId, Guid packageTypeId, string name, string description, decimal price, int quantity, decimal weightKg, DateTime pickupStart, DateTime pickupEnd)
        {
            var package = new Package
            {
                Id = id, BusinessId = businessId, PackageTypeId = packageTypeId, Name = name, Description = description,
                Price = price, Quantity = quantity, WeightKg = weightKg, PickupStart = pickupStart, PickupEnd = pickupEnd,
            };
            packagesById[id] = package;
            newPackages.Add(package);
            return package;
        }

        var surpriseBag = new Guid("22222222-0000-0000-0000-000000000001");
        var mealBox     = new Guid("22222222-0000-0000-0000-000000000002");
        var breadBag    = new Guid("22222222-0000-0000-0000-000000000003");
        var pastryBox   = new Guid("22222222-0000-0000-0000-000000000005");

        DateTime PastAt(int daysAgo, int hour, int minute) => now.Date.AddDays(-daysAgo).AddHours(hour).AddMinutes(minute);

        var hist1 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000001"), b1, mealBox,     "Lunch Break Meal Box",    "Midday leftovers from the kitchen's lunch service.", 9.50m, 5,  1.2m, PastAt(9, 12, 0), PastAt(9, 13, 0));
        var hist2 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000002"), b1, surpriseBag, "Full-Time Surprise Bag",  "Evening surplus, sold out fast.",                     8.99m, 6,  1.5m, PastAt(7, 18, 0), PastAt(7, 19, 0));
        var hist3 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000003"), b1, mealBox,     "Second Half Meal Box",    "A second batch from the same evening service.",      9.99m, 4,  1.2m, PastAt(6, 18, 0), PastAt(6, 19, 0));
        var hist4 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000004"), b1, surpriseBag, "Late Kick-Off Bag",       "Whatever's left once the evening rush ends.",        7.99m, 5,  1.5m, PastAt(5, 19, 0), PastAt(5, 20, 0));
        var hist5 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000005"), b2, pastryBox,   "Midday Pastry Box",       "Leftover pastries from the lunch counter.",          6.50m, 8,  0.6m, PastAt(8, 13, 0), PastAt(8, 14, 0));
        var hist6 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000006"), b2, breadBag,    "Sundown Bread Bag",       "Bread and rolls nearing the end of the day.",        5.50m, 10, 1.4m, PastAt(4, 17, 0), PastAt(4, 18, 0));
        var hist7 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000007"), b2, mealBox,     "Closing Time Meal Box",   "The last meal boxes before closing.",                9.75m, 6,  1.2m, PastAt(3, 18, 0), PastAt(3, 19, 0));
        var hist8 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000008"), b3, surpriseBag, "Derby Night Bag",         "Match-night surplus from the derby-day kitchen.",    8.50m, 5,  1.5m, PastAt(2, 19, 0), PastAt(2, 20, 0));
        var hist9 = HistoricalPackage(new Guid("77777777-0000-0000-0000-000000000009"), b3, mealBox,     "Last Call Meal Box",      "The final meal boxes of the night.",                10.50m, 4,  1.2m, PastAt(1, 20, 0), PastAt(1, 21, 0));

        var histCompleted1 = MakeOrder(b1, hist1.Id, 3, OrderStatuses.Completed, PastAt(9, 12, 15));
        var histCompleted2 = MakeOrder(b1, hist2.Id, 6, OrderStatuses.Completed, PastAt(7, 18, 10));
        var histCompleted3 = MakeOrder(b1, hist3.Id, 3, OrderStatuses.Completed, PastAt(6, 18, 20));
        var histCompleted4 = MakeOrder(b1, hist4.Id, 2, OrderStatuses.Completed, PastAt(5, 19, 5));
        var histCompleted5 = MakeOrder(b2, hist5.Id, 5, OrderStatuses.Completed, PastAt(8, 13, 10));
        var histCompleted6 = MakeOrder(b2, hist6.Id, 7, OrderStatuses.Completed, PastAt(4, 17, 25));
        var histCompleted7 = MakeOrder(b2, hist7.Id, 6, OrderStatuses.Completed, PastAt(3, 18, 5));
        var histCompleted8 = MakeOrder(b3, hist8.Id, 4, OrderStatuses.Completed, PastAt(2, 19, 15));
        var histCompleted9 = MakeOrder(b3, hist9.Id, 1, OrderStatuses.Completed, PastAt(1, 20, 30));

        db.Packages.AddRange(newPackages);
        db.Orders.AddRange(oldCompleted, midCompleted, recentCompleted, cancelled, noShow, confirmed, pending,
            histCompleted1, histCompleted2, histCompleted3, histCompleted4, histCompleted5, histCompleted6, histCompleted7, histCompleted8, histCompleted9);
        db.Payments.AddRange(payments);
        db.OrderPickupPasses.AddRange(pickupPasses);

        db.Favorites.AddRange(
            new Favorite { Id = Guid.NewGuid(), UserId = demoCustomer.Id, BusinessId = b1, CreatedAt = now },
            new Favorite { Id = Guid.NewGuid(), UserId = demoCustomer.Id, BusinessId = b7, CreatedAt = now },
            new Favorite { Id = Guid.NewGuid(), UserId = demoCustomer.Id, BusinessId = b10, CreatedAt = now }
        );

        // Tagged to the packages those orders actually contain, so the package-level review tag
        // has real data on a fresh database instead of only the business-level default.
        db.Reviews.AddRange(
            new Review { Id = Guid.NewGuid(), BusinessId = b1, UserId = demoCustomer.Id, PackageId = recentCompleted.OrderPackages.First().PackageId, Rating = 5, Comment = "Great surprise bag, saved us from cooking twice!", CreatedAt = now.AddDays(-6) },
            new Review { Id = Guid.NewGuid(), BusinessId = b3, UserId = demoCustomer.Id, PackageId = midCompleted.OrderPackages.First().PackageId, Rating = 4, Comment = "Solid portion, will order again.", CreatedAt = now.AddDays(-9) }
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