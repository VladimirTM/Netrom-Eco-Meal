using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandsAndWebhookApiKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BrandId",
                table: "Businesses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookApiKeyHash",
                table: "Businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WebhookApiKeyLastUsedAt",
                table: "Businesses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Brands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrandFavorites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    BrandId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrandFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrandFavorites_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BrandFavorites_Brands_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_BrandId",
                table: "Businesses",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_WebhookApiKeyHash",
                table: "Businesses",
                column: "WebhookApiKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BrandFavorites_BrandId",
                table: "BrandFavorites",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_BrandFavorites_UserId_BrandId",
                table: "BrandFavorites",
                columns: new[] { "UserId", "BrandId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Businesses_Brands_BrandId",
                table: "Businesses",
                column: "BrandId",
                principalTable: "Brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Businesses_Brands_BrandId",
                table: "Businesses");

            migrationBuilder.DropTable(
                name: "BrandFavorites");

            migrationBuilder.DropTable(
                name: "Brands");

            migrationBuilder.DropIndex(
                name: "IX_Businesses_BrandId",
                table: "Businesses");

            migrationBuilder.DropIndex(
                name: "IX_Businesses_WebhookApiKeyHash",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "BrandId",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "WebhookApiKeyHash",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "WebhookApiKeyLastUsedAt",
                table: "Businesses");
        }
    }
}
