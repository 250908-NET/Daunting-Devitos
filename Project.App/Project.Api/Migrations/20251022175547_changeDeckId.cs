using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Project.Api.Migrations
{
    /// <inheritdoc />
    public partial class changeDeckId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "isPublic",
                table: "Rooms",
                newName: "IsPublic");

            migrationBuilder.RenameColumn(
                name: "isActive",
                table: "Rooms",
                newName: "IsActive");

            migrationBuilder.AlterColumn<string>(
                name: "DeckId",
                table: "Rooms",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "GameConfig",
                table: "Rooms",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Rooms",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<long>(
                name: "BalanceDelta",
                table: "RoomPlayers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GameConfig",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "BalanceDelta",
                table: "RoomPlayers");

            migrationBuilder.RenameColumn(
                name: "IsPublic",
                table: "Rooms",
                newName: "isPublic");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Rooms",
                newName: "isActive");

            migrationBuilder.AlterColumn<int>(
                name: "DeckId",
                table: "Rooms",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
