using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class FixChatMemberForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChatId1",
                table: "ChatMembers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMembers_ChatId1",
                table: "ChatMembers",
                column: "ChatId1");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMembers_Chats_ChatId1",
                table: "ChatMembers",
                column: "ChatId1",
                principalTable: "Chats",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMembers_Chats_ChatId1",
                table: "ChatMembers");

            migrationBuilder.DropIndex(
                name: "IX_ChatMembers_ChatId1",
                table: "ChatMembers");

            migrationBuilder.DropColumn(
                name: "ChatId1",
                table: "ChatMembers");
        }
    }
}
