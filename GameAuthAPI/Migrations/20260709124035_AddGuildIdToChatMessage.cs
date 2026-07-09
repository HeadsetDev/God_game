using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAuthAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildIdToChatMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GuildId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_GuildId",
                table: "ChatMessages",
                column: "GuildId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Guilds_GuildId",
                table: "ChatMessages",
                column: "GuildId",
                principalTable: "Guilds",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Guilds_GuildId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_GuildId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "GuildId",
                table: "ChatMessages");
        }
    }
}
