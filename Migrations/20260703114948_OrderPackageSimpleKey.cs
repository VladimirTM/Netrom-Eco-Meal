using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class OrderPackageSimpleKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderPackages",
                table: "OrderPackages");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "OrderPackages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderPackages",
                table: "OrderPackages",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_OrderPackages_OrderId",
                table: "OrderPackages",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderPackages",
                table: "OrderPackages");

            migrationBuilder.DropIndex(
                name: "IX_OrderPackages_OrderId",
                table: "OrderPackages");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "OrderPackages");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderPackages",
                table: "OrderPackages",
                columns: new[] { "OrderId", "PackageId" });
        }
    }
}
