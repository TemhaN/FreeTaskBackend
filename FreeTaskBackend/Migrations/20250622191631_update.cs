using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class update : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChatId1",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "ClientProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "ClientProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "ClientProfiles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReadAt",
                table: "ChatMembers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_ChatId1",
                table: "Messages",
                column: "ChatId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Chats_ChatId1",
                table: "Messages",
                column: "ChatId1",
                principalTable: "Chats",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Chats_ChatId1",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_ChatId1",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ChatId1",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "ClientProfiles");

            migrationBuilder.DropColumn(
                name: "LastReadAt",
                table: "ChatMembers");
        }
    }
}
