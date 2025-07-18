using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class CreatePortfolioItemsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Portfolio",
                table: "FreelancerProfiles");

            migrationBuilder.CreateTable(
                name: "PortfolioItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FreelancerProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioItems_FreelancerProfiles_FreelancerProfileId",
                        column: x => x.FreelancerProfileId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_FreelancerProfileId",
                table: "PortfolioItems",
                column: "FreelancerProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioItems");

            migrationBuilder.AddColumn<string>(
                name: "Portfolio",
                table: "FreelancerProfiles",
                type: "jsonb",
                nullable: false,
                defaultValue: "");
        }
    }
}
