using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStaleBusinessSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The 2026-07-03/07-10 SeedData/MoreSeedData migrations baked in a fixed set of
            // Cluj-Napoca demo businesses, so DbSeeder's "only seed when the table is empty" check
            // never fires on a database that already applied them. Deleting the 8 known stale IDs
            // (cascading their Packages away too) clears the way for DbSeeder to insert the current
            // Timișoara/World Cup seed data instead. Guarded with NOT EXISTS on Orders/Reviews so a
            // real customer's order/review against one of these businesses is never cascade-deleted
            // with it — that ID is skipped instead, leaving its stale name/description in place.
            migrationBuilder.Sql("""
                DELETE FROM "Businesses" b WHERE b."Id" IN (
                    '44444444-0000-0000-0000-000000000001',
                    '44444444-0000-0000-0000-000000000002',
                    '44444444-0000-0000-0000-000000000003',
                    '44444444-0000-0000-0000-000000000004',
                    '44444444-0000-0000-0000-000000000005',
                    '44444444-0000-0000-0000-000000000006',
                    '44444444-0000-0000-0000-000000000007',
                    '44444444-0000-0000-0000-000000000008'
                )
                AND NOT EXISTS (SELECT 1 FROM "Orders" o WHERE o."BusinessId" = b."Id")
                AND NOT EXISTS (SELECT 1 FROM "Reviews" r WHERE r."BusinessId" = b."Id");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible: the deleted rows were stale demo data, not user data.
        }
    }
}
