using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAuthAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddGuildsAndGroupQuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGroupQuest",
                table: "Quests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RequiredPlayers",
                table: "Quests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeaderId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestParticipants",
                columns: table => new
                {
                    QuestId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestParticipants", x => new { x.QuestId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_QuestParticipants_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuestParticipants_Quests_QuestId",
                        column: x => x.QuestId,
                        principalTable: "Quests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerGuilds",
                columns: table => new
                {
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    GuildId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGuilds", x => new { x.PlayerId, x.GuildId });
                    table.ForeignKey(
                        name: "FK_PlayerGuilds_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerGuilds_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerGuilds_GuildId",
                table: "PlayerGuilds",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestParticipants_PlayerId",
                table: "QuestParticipants",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerGuilds");

            migrationBuilder.DropTable(
                name: "QuestParticipants");

            migrationBuilder.DropTable(
                name: "Guilds");

            migrationBuilder.DropColumn(
                name: "IsGroupQuest",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "RequiredPlayers",
                table: "Quests");
        }
    }
}
