using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Netrom_Eco_Meal.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStalePackageSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The 2026-07-04 SeedData/MoreSeedData migrations baked demo packages in with a fixed
            // calendar date that goes stale the day after it's applied. Seeding moved to DbSeeder
            // (see MoveSeederToDbSeeder), which picks pickup times relative to "today" instead —
            // but it only seeds when the Packages table is empty, so the old hardcoded rows have to
            // go first for that logic to ever run on a database that already applied those migrations.
            migrationBuilder.Sql("DELETE FROM \"Packages\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible: the deleted rows were stale demo data, not user data.
        }
    }
}
