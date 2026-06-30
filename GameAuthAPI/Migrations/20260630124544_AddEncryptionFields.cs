using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameAuthAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddEncryptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Message",
                table: "ChatMessages",
                newName: "MessageEncrypted");

            migrationBuilder.AddColumn<string>(
                name: "AddressEncrypted",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailEncrypted",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneEncrypted",
                table: "Players",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuctionLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Seller = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    WinnerId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionLots", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionLots");

            migrationBuilder.DropColumn(
                name: "AddressEncrypted",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "EmailEncrypted",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "PhoneEncrypted",
                table: "Players");

            migrationBuilder.RenameColumn(
                name: "MessageEncrypted",
                table: "ChatMessages",
                newName: "Message");
        }
    }
}
