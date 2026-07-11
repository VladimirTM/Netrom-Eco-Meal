using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessManagerAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
