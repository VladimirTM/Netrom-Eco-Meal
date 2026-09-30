using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetromEcoMeal.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageDonationAndImpactWidget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DonatedAt",
                table: "Packages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DonationOfferedAt",
                table: "Packages",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DonatedAt",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "DonationOfferedAt",
                table: "Packages");
        }
    }
}
