using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsForSpecialLeaveRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE special_leave_requests ENABLE ROW LEVEL SECURITY;
                  CREATE POLICY tenant_isolation ON special_leave_requests
                  USING (space_id::text = current_setting('app.current_space_id', TRUE));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP POLICY IF EXISTS tenant_isolation ON special_leave_requests;
                  ALTER TABLE special_leave_requests DISABLE ROW LEVEL SECURITY;");
        }
    }
}
