using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamIdAndTypeToBids : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "Bids",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Bids",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Bids",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TeamId",
                table: "Bids",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Bids",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_TeamId",
                table: "Bids",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_Teams_TeamId",
                table: "Bids",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bids_Teams_TeamId",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Bids_TeamId",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Bids");

            migrationBuilder.AlterColumn<string>(
                name: "Comment",
                table: "Bids",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Bids",
                type: "numeric",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);
        }
    }
}
