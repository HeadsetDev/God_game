using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAuthAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddInitialData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                    table: "Locations",
                    columns: new[] { "Name", "Description" },
                    values: new object[] { "Starting Location", "The starting point for all players." });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
