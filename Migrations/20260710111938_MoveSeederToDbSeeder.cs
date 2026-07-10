using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class MoveSeederToDbSeeder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "BusinessTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), "Restaurant" },
                    { new Guid("11111111-0000-0000-0000-000000000002"), "Bakery" },
                    { new Guid("11111111-0000-0000-0000-000000000003"), "Cafe" },
                    { new Guid("11111111-0000-0000-0000-000000000004"), "Grocery Store" },
                    { new Guid("11111111-0000-0000-0000-000000000005"), "Food Truck" }
                });

            migrationBuilder.InsertData(
                table: "PackageTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("22222222-0000-0000-0000-000000000001"), "Surprise Bag" },
                    { new Guid("22222222-0000-0000-0000-000000000002"), "Meal Box" },
                    { new Guid("22222222-0000-0000-0000-000000000003"), "Bread Bag" },
                    { new Guid("22222222-0000-0000-0000-000000000004"), "Veggie Box" },
                    { new Guid("22222222-0000-0000-0000-000000000005"), "Pastry Box" }
                });

            migrationBuilder.InsertData(
                table: "Statuses",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("33333333-0000-0000-0000-000000000001"), "Pending" },
                    { new Guid("33333333-0000-0000-0000-000000000002"), "Confirmed" },
                    { new Guid("33333333-0000-0000-0000-000000000003"), "Completed" },
                    { new Guid("33333333-0000-0000-0000-000000000004"), "Cancelled" }
                });

            migrationBuilder.InsertData(
                table: "Businesses",
                columns: new[] { "Id", "Address", "BusinessTypeId", "Description", "ImageUrl", "Name" },
                values: new object[,]
                {
                    { new Guid("44444444-0000-0000-0000-000000000001"), "12 Oak Street, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000001"), "Farm-to-table restaurant with daily surplus meals.", null, "Green Fork" },
                    { new Guid("44444444-0000-0000-0000-000000000002"), "5 Bread Lane, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000002"), "Artisan bakery with fresh bread and pastries every morning.", null, "Sunrise Bakery" },
                    { new Guid("44444444-0000-0000-0000-000000000003"), "88 Central Boulevard, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000003"), "Specialty coffee shop with homemade snacks.", null, "The Daily Grind" },
                    { new Guid("44444444-0000-0000-0000-000000000004"), "3 Unirii Square, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000001"), "Traditional Romanian home-cooking with generous portions.", null, "La Mama" },
                    { new Guid("44444444-0000-0000-0000-000000000005"), "21 Flour Street, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000002"), "Family-run bakery specialising in sourdough and sweet rolls.", null, "Wheat & Wonder" },
                    { new Guid("44444444-0000-0000-0000-000000000006"), "7 Garden Alley, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000003"), "Cosy cafe with seasonal drinks and homemade cakes.", null, "Bloom & Brew" },
                    { new Guid("44444444-0000-0000-0000-000000000007"), "55 Market Road, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000004"), "Local grocery store with fresh produce and ready-made meals.", null, "FreshMart" },
                    { new Guid("44444444-0000-0000-0000-000000000008"), "Central Park, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000005"), "Food truck serving global street-food with a local twist.", null, "Street Spoon" }
                });

            migrationBuilder.InsertData(
                table: "Packages",
                columns: new[] { "Id", "BusinessId", "Description", "ImageUrl", "Name", "PackageTypeId", "PickupEnd", "PickupStart", "Price", "Quantity" },
                values: new object[,]
                {
                    { new Guid("55555555-0000-0000-0000-000000000001"), new Guid("44444444-0000-0000-0000-000000000001"), "A surprise selection of today's leftover dishes — always delicious.", null, "Green Fork Surprise Bag", new Guid("22222222-0000-0000-0000-000000000001"), new DateTime(2026, 7, 4, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 17, 0, 0, 0, DateTimeKind.Utc), 12.99m, 5 },
                    { new Guid("55555555-0000-0000-0000-000000000002"), new Guid("44444444-0000-0000-0000-000000000001"), "A full meal box with a main course and side dish.", null, "Green Fork Meal Box", new Guid("22222222-0000-0000-0000-000000000002"), new DateTime(2026, 7, 4, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 17, 0, 0, 0, DateTimeKind.Utc), 9.99m, 3 },
                    { new Guid("55555555-0000-0000-0000-000000000003"), new Guid("44444444-0000-0000-0000-000000000002"), "Assorted fresh breads and pastries from the day.", null, "Sunrise Bread Bag", new Guid("22222222-0000-0000-0000-000000000003"), new DateTime(2026, 7, 4, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 16, 0, 0, 0, DateTimeKind.Utc), 6.50m, 10 },
                    { new Guid("55555555-0000-0000-0000-000000000004"), new Guid("44444444-0000-0000-0000-000000000003"), "Leftover sandwiches, muffins, and snacks from the cafe.", null, "Daily Grind Surprise Bag", new Guid("22222222-0000-0000-0000-000000000001"), new DateTime(2026, 7, 4, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 18, 0, 0, 0, DateTimeKind.Utc), 7.99m, 4 },
                    { new Guid("55555555-0000-0000-0000-000000000005"), new Guid("44444444-0000-0000-0000-000000000004"), "A hearty Romanian meal with soup, main, and dessert.", null, "La Mama Meal Box", new Guid("22222222-0000-0000-0000-000000000002"), new DateTime(2026, 7, 4, 20, 30, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 17, 30, 0, 0, DateTimeKind.Utc), 11.50m, 6 },
                    { new Guid("55555555-0000-0000-0000-000000000006"), new Guid("44444444-0000-0000-0000-000000000004"), "Mystery mix of leftover homemade Romanian dishes.", null, "La Mama Surprise Bag", new Guid("22222222-0000-0000-0000-000000000001"), new DateTime(2026, 7, 4, 21, 30, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 19, 0, 0, 0, DateTimeKind.Utc), 8.50m, 4 },
                    { new Guid("55555555-0000-0000-0000-000000000007"), new Guid("44444444-0000-0000-0000-000000000005"), "Six assorted pastries: croissants, cinnamon rolls, and more.", null, "Wheat & Wonder Pastry Box", new Guid("22222222-0000-0000-0000-000000000005"), new DateTime(2026, 7, 4, 18, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 15, 0, 0, 0, DateTimeKind.Utc), 8.00m, 8 },
                    { new Guid("55555555-0000-0000-0000-000000000008"), new Guid("44444444-0000-0000-0000-000000000005"), "End-of-day selection of sourdough, rye, and whole wheat loaves.", null, "Wheat & Wonder Bread Bag", new Guid("22222222-0000-0000-0000-000000000003"), new DateTime(2026, 7, 4, 19, 30, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 16, 30, 0, 0, DateTimeKind.Utc), 5.00m, 12 },
                    { new Guid("55555555-0000-0000-0000-000000000009"), new Guid("44444444-0000-0000-0000-000000000006"), "Leftover cakes, cookies, and quiches of the day.", null, "Bloom & Brew Surprise Bag", new Guid("22222222-0000-0000-0000-000000000001"), new DateTime(2026, 7, 4, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 17, 0, 0, 0, DateTimeKind.Utc), 6.99m, 5 },
                    { new Guid("55555555-0000-0000-0000-000000000010"), new Guid("44444444-0000-0000-0000-000000000007"), "Seasonal vegetables and fruit nearing best-before — still fresh.", null, "FreshMart Veggie Box", new Guid("22222222-0000-0000-0000-000000000004"), new DateTime(2026, 7, 4, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 16, 0, 0, 0, DateTimeKind.Utc), 5.50m, 15 },
                    { new Guid("55555555-0000-0000-0000-000000000011"), new Guid("44444444-0000-0000-0000-000000000008"), "A full street-food meal — tacos, gyros, or noodles (surprise pick).", null, "Street Spoon Meal Box", new Guid("22222222-0000-0000-0000-000000000002"), new DateTime(2026, 7, 4, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 18, 30, 0, 0, DateTimeKind.Utc), 10.00m, 7 },
                    { new Guid("55555555-0000-0000-0000-000000000012"), new Guid("44444444-0000-0000-0000-000000000008"), "Mixed snacks, sides, and small bites leftover from the day.", null, "Street Spoon Surprise Bag", new Guid("22222222-0000-0000-0000-000000000001"), new DateTime(2026, 7, 4, 22, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 20, 0, 0, 0, DateTimeKind.Utc), 5.99m, 6 }
                });
        }
    }
}
