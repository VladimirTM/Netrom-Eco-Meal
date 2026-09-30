using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetromEcoMeal.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaderboardOptIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowOnLeaderboard",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShowOnLeaderboard",
                table: "AspNetUsers");
        }
    }
}
