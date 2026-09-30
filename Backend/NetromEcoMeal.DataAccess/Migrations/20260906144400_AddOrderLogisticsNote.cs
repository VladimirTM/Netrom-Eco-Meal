using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetromEcoMeal.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderLogisticsNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogisticsNote",
                table: "PendingCheckouts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogisticsNote",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogisticsNote",
                table: "PendingCheckouts");

            migrationBuilder.DropColumn(
                name: "LogisticsNote",
                table: "Orders");
        }
    }
}
