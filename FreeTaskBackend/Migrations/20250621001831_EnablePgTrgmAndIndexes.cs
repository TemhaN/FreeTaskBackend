using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreeTaskBackend.Migrations
{
    /// <inheritdoc />
    public partial class EnablePgTrgmAndIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
                CREATE INDEX idx_freelancer_trgm_bio ON ""FreelancerProfiles"" USING GIN (""Bio"" gin_trgm_ops);
                CREATE INDEX idx_users_name_trgm ON ""Users"" USING GIN (""Name"" gin_trgm_ops);
                CREATE INDEX idx_users_email_trgm ON ""Users"" USING GIN (""Email"" gin_trgm_ops);
                CREATE INDEX idx_portfolio_trgm_description ON ""PortfolioItem"" USING GIN (""Description"" gin_trgm_ops);
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_freelancer_trgm_bio;
                DROP INDEX IF EXISTS idx_users_name_trgm;
                DROP INDEX IF EXISTS idx_users_email_trgm;
                DROP INDEX IF EXISTS idx_portfolio_trgm_description;
            ");
        }
    }
}
