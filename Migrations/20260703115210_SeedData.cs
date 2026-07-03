using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "BusinessTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), "Restaurant" },
                    { new Guid("11111111-0000-0000-0000-000000000002"), "Bakery" },
                    { new Guid("11111111-0000-0000-0000-000000000003"), "Cafe" }
                });

            migrationBuilder.InsertData(
                table: "PackageTypes",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("22222222-0000-0000-0000-000000000001"), "Surprise Bag" },
                    { new Guid("22222222-0000-0000-0000-000000000002"), "Meal Box" },
                    { new Guid("22222222-0000-0000-0000-000000000003"), "Bread Bag" }
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
                    { new Guid("44444444-0000-0000-0000-000000000003"), "88 Central Boulevard, Cluj-Napoca", new Guid("11111111-0000-0000-0000-000000000003"), "Specialty coffee shop with homemade snacks.", null, "The Daily Grind" }
                });

            migrationBuilder.InsertData(
                table: "Packages",
                columns: new[] { "Id", "BusinessId", "Description", "ImageUrl", "Name", "PackageTypeId", "PickupEnd", "PickupStart", "Price", "Quantity" },
                values: new object[,]
                {
                    { new Guid("55555555-0000-0000-0000-000000000001"), new Guid("44444444-0000-0000-0000-000000000001"), "A surprise selection of today's leftover dishes — always delicious.", null, "Green Fork Surprise Bag", new Guid("22222222-0000-0000-0000-000000000001"), new DateTime(2026, 7, 4, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 17, 0, 0, 0, DateTimeKind.Utc), 12.99m, 5 },
                    { new Guid("55555555-0000-0000-0000-000000000002"), new Guid("44444444-0000-0000-0000-000000000001"), "A full meal box with a main course and side dish.", null, "Green Fork Meal Box", new Guid("22222222-0000-0000-0000-000000000002"), new DateTime(2026, 7, 4, 20, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 17, 0, 0, 0, DateTimeKind.Utc), 9.99m, 3 },
                    { new Guid("55555555-0000-0000-0000-000000000003"), new Guid("44444444-0000-0000-0000-000000000002"), "Assorted fresh breads and pastries from the day.", null, "Sunrise Bread Bag", new Guid("22222222-0000-0000-0000-000000000003"), new DateTime(2026, 7, 4, 19, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 16, 0, 0, 0, DateTimeKind.Utc), 6.50m, 10 },
                    { new Guid("55555555-0000-0000-0000-000000000004"), new Guid("44444444-0000-0000-0000-000000000003"), "Leftover sandwiches, muffins, and snacks from the cafe.", null, "Daily Grind Surprise Bag", new Guid("22222222-0000-0000-0000-000000000001"), new DateTime(2026, 7, 4, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 4, 18, 0, 0, 0, DateTimeKind.Utc), 7.99m, 4 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: new Guid("55555555-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: new Guid("55555555-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: new Guid("55555555-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Packages",
                keyColumn: "Id",
                keyValue: new Guid("55555555-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Statuses",
                keyColumn: "Id",
                keyValue: new Guid("33333333-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Statuses",
                keyColumn: "Id",
                keyValue: new Guid("33333333-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Statuses",
                keyColumn: "Id",
                keyValue: new Guid("33333333-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Statuses",
                keyColumn: "Id",
                keyValue: new Guid("33333333-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Businesses",
                keyColumn: "Id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Businesses",
                keyColumn: "Id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Businesses",
                keyColumn: "Id",
                keyValue: new Guid("44444444-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "PackageTypes",
                keyColumn: "Id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "PackageTypes",
                keyColumn: "Id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "PackageTypes",
                keyColumn: "Id",
                keyValue: new Guid("22222222-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "BusinessTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "BusinessTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "BusinessTypes",
                keyColumn: "Id",
                keyValue: new Guid("11111111-0000-0000-0000-000000000003"));
        }
    }
}
