using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarAndPortfolio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Portfolio",
                table: "FreelancerProfiles");

            // First create a temporary column for skills
            migrationBuilder.AddColumn<List<string>>(
                name: "SkillsTemp",
                table: "FreelancerProfiles",
                type: "text[]",
                nullable: false);

            // Copy data from old column to new one
            migrationBuilder.Sql(@"
                UPDATE ""FreelancerProfiles""
                SET ""SkillsTemp"" = ARRAY(
                    SELECT jsonb_array_elements_text(""Skills"")::text
                )
            ");

            // Drop the old column
            migrationBuilder.DropColumn(
                name: "Skills",
                table: "FreelancerProfiles");

            // Rename the temporary column
            migrationBuilder.RenameColumn(
                name: "SkillsTemp",
                table: "FreelancerProfiles",
                newName: "Skills");

            // Convert Level from text to integer with explicit conversion
            migrationBuilder.Sql(@"
                ALTER TABLE ""FreelancerProfiles""
                ALTER COLUMN ""Level"" TYPE integer
                USING CASE
                    WHEN ""Level"" = 'Newbie' THEN 0
                    WHEN ""Level"" = 'Specialist' THEN 1
                    WHEN ""Level"" = 'Expert' THEN 2
                    ELSE 0
                END
            ");

            migrationBuilder.AddColumn<string>(
                name: "AvatarUrl",
                table: "FreelancerProfiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "FreelancerProfiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "PortfolioItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    FreelancerProfileId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioItem_FreelancerProfiles_FreelancerProfileId",
                        column: x => x.FreelancerProfileId,
                        principalTable: "FreelancerProfiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItem_FreelancerProfileId",
                table: "PortfolioItem",
                column: "FreelancerProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioItem");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "FreelancerProfiles");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "FreelancerProfiles");

            // Convert Level back to text
            migrationBuilder.Sql(@"
                ALTER TABLE ""FreelancerProfiles""
                ALTER COLUMN ""Level"" TYPE text
                USING CASE
                    WHEN ""Level"" = 0 THEN 'Newbie'
                    WHEN ""Level"" = 1 THEN 'Specialist'
                    WHEN ""Level"" = 2 THEN 'Expert'
                    ELSE 'Newbie'
                END
            ");

            // For skills, we'll follow a similar approach
            migrationBuilder.AddColumn<JsonDocument>(
                name: "SkillsTemp",
                table: "FreelancerProfiles",
                type: "jsonb",
                nullable: false);

            migrationBuilder.Sql(@"
                UPDATE ""FreelancerProfiles""
                SET ""SkillsTemp"" = to_jsonb(""Skills"")
            ");

            migrationBuilder.DropColumn(
                name: "Skills",
                table: "FreelancerProfiles");

            migrationBuilder.RenameColumn(
                name: "SkillsTemp",
                table: "FreelancerProfiles",
                newName: "Skills");

            migrationBuilder.AddColumn<JsonDocument>(
                name: "Portfolio",
                table: "FreelancerProfiles",
                type: "jsonb",
                nullable: false);
        }
    }
}