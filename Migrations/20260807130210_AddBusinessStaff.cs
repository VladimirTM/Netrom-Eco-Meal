using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessStaff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BusinessStaff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessStaff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessStaff_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessStaff_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessStaff_BusinessId_UserId",
                table: "BusinessStaff",
                columns: new[] { "BusinessId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessStaff_UserId",
                table: "BusinessStaff",
                column: "UserId");

            // Carries forward every existing single-manager assignment into the new join table
            // before the old column disappears below — an upgrade of a real database must not
            // silently lose these relationships (see the seeding-architecture lesson: migrations,
            // not DbSeeder, are responsible for backfilling data forward).
            migrationBuilder.Sql(
                """
                INSERT INTO "BusinessStaff" ("Id", "BusinessId", "UserId", "AssignedAt")
                SELECT gen_random_uuid(), "Id", "ManagerId", now()
                FROM "Businesses"
                WHERE "ManagerId" IS NOT NULL
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Businesses_AspNetUsers_ManagerId",
                table: "Businesses");

            migrationBuilder.DropIndex(
                name: "IX_Businesses_ManagerId",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Businesses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BusinessStaff");

            migrationBuilder.AddColumn<string>(
                name: "ManagerId",
                table: "Businesses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_ManagerId",
                table: "Businesses",
                column: "ManagerId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Businesses_AspNetUsers_ManagerId",
                table: "Businesses",
                column: "ManagerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
