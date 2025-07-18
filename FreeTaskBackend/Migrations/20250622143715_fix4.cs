using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class fix4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMembers_Chats_ChatId1",
                table: "ChatMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_ChatMembers_ChatId1",
                table: "ChatMembers");

            migrationBuilder.DropColumn(
                name: "ChatId1",
                table: "ChatMembers");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_InvoiceId",
                table: "Payments",
                column: "InvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id");
        }
    }
}
