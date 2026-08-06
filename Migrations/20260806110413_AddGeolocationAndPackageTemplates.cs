using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class AddGeolocationAndPackageTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Packages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Businesses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Businesses",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PackageTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric", nullable: false),
                    DietaryTags = table.Column<List<string>>(type: "text[]", nullable: false),
                    PickupStartTimeUtc = table.Column<TimeSpan>(type: "interval", nullable: false),
                    PickupEndTimeUtc = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastGeneratedDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageTemplates_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageTemplates_PackageTypes_PackageTypeId",
                        column: x => x.PackageTypeId,
                        principalTable: "PackageTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Packages_TemplateId",
                table: "Packages",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageTemplates_BusinessId",
                table: "PackageTemplates",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageTemplates_PackageTypeId",
                table: "PackageTemplates",
                column: "PackageTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_PackageTemplates_TemplateId",
                table: "Packages",
                column: "TemplateId",
                principalTable: "PackageTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packages_PackageTemplates_TemplateId",
                table: "Packages");

            migrationBuilder.DropTable(
                name: "PackageTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Packages_TemplateId",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Packages");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Businesses");
        }
    }
}
