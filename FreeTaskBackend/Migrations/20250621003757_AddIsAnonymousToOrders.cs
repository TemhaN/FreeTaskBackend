using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddIsAnonymousToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAnonymous",
                table: "ClientProfiles");

            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymous",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAnonymous",
                table: "ClientProfiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropColumn(
                name: "IsAnonymous",
                table: "Orders");
        }
    }
}
