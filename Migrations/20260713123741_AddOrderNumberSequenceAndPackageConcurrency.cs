using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumberSequenceAndPackageConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "order_numbers");

            // Existing databases already have Orders rows numbered by the old app-side MAX+1
            // logic — point the new sequence past the highest one so it doesn't immediately
            // collide with them.
            migrationBuilder.Sql(
                "SELECT setval('order_numbers', COALESCE((SELECT MAX(\"OrderNumber\") FROM \"Orders\"), 0) + 1, false);");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Packages",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AlterColumn<int>(
                name: "OrderNumber",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValueSql: "nextval('order_numbers')",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Packages");

            migrationBuilder.DropSequence(
                name: "order_numbers");

            migrationBuilder.AlterColumn<int>(
                name: "OrderNumber",
                table: "Orders",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValueSql: "nextval('order_numbers')");
        }
    }
}
