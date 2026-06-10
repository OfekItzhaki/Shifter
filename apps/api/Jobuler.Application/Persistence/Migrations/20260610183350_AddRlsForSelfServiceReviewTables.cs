using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsForSelfServiceReviewTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE shift_absence_reports ENABLE ROW LEVEL SECURITY;
                  CREATE POLICY tenant_isolation ON shift_absence_reports
                  USING (space_id::text = current_setting('app.current_space_id', TRUE));");

            migrationBuilder.Sql(
                @"ALTER TABLE shift_change_requests ENABLE ROW LEVEL SECURITY;
                  CREATE POLICY tenant_isolation ON shift_change_requests
                  USING (space_id::text = current_setting('app.current_space_id', TRUE));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP POLICY IF EXISTS tenant_isolation ON shift_change_requests;
                  ALTER TABLE shift_change_requests DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.Sql(
                @"DROP POLICY IF EXISTS tenant_isolation ON shift_absence_reports;
                  ALTER TABLE shift_absence_reports DISABLE ROW LEVEL SECURITY;");
        }
    }
}
