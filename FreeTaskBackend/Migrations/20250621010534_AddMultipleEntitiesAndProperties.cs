using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleEntitiesAndProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "CertificateUrls",
                table: "Users",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationDocumentUrl",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FreelancerId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "QuickReplies",
                table: "FreelancerProfiles",
                type: "text[]",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_FreelancerId",
                table: "Orders",
                column: "FreelancerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_FreelancerId",
                table: "Orders",
                column: "FreelancerId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_FreelancerId",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Orders_FreelancerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CertificateUrls",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerificationDocumentUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FreelancerId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "QuickReplies",
                table: "FreelancerProfiles");
        }
    }
}
