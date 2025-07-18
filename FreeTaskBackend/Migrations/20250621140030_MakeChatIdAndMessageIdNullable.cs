using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class MakeChatIdAndMessageIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileAttachments_Chats_ChatId",
                table: "FileAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_FileAttachments_Messages_MessageId",
                table: "FileAttachments");

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                table: "FileAttachments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChatId",
                table: "FileAttachments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_FileAttachments_Chats_ChatId",
                table: "FileAttachments",
                column: "ChatId",
                principalTable: "Chats",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FileAttachments_Messages_MessageId",
                table: "FileAttachments",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FileAttachments_Chats_ChatId",
                table: "FileAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_FileAttachments_Messages_MessageId",
                table: "FileAttachments");

            migrationBuilder.AlterColumn<Guid>(
                name: "MessageId",
                table: "FileAttachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ChatId",
                table: "FileAttachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_FileAttachments_Chats_ChatId",
                table: "FileAttachments",
                column: "ChatId",
                principalTable: "Chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FileAttachments_Messages_MessageId",
                table: "FileAttachments",
                column: "MessageId",
                principalTable: "Messages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
