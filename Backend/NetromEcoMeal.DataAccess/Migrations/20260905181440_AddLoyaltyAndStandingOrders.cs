using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetromEcoMeal.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyAndStandingOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LoyaltyDiscountAmount",
                table: "Businesses",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoyaltyPunchThreshold",
                table: "Businesses",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StandingOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    PackageTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DietaryTag = table.Column<string>(type: "text", nullable: true),
                    MaxWeeklySpend = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandingOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StandingOrders_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StandingOrders_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StandingOrders_PackageTypes_PackageTypeId",
                        column: x => x.PackageTypeId,
                        principalTable: "PackageTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StandingOrders_BusinessId",
                table: "StandingOrders",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_StandingOrders_PackageTypeId",
                table: "StandingOrders",
                column: "PackageTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StandingOrders_UserId",
                table: "StandingOrders",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StandingOrders");

            migrationBuilder.DropColumn(
                name: "LoyaltyDiscountAmount",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "LoyaltyPunchThreshold",
                table: "Businesses");
        }
    }
}
