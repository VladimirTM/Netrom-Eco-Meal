using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageLevelReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PackageId",
                table: "Reviews",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PackageId",
                table: "Reviews",
                column: "PackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Packages_PackageId",
                table: "Reviews",
                column: "PackageId",
                principalTable: "Packages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Packages_PackageId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_PackageId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "PackageId",
                table: "Reviews");
        }
    }
}
