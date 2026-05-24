using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaceManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "spaces",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "management_timeout_minutes",
                table: "spaces",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<int>(
                name: "permission_level",
                table: "space_memberships",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "deleted_by_space_deletion",
                table: "groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "space_home_leave_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    balance_value = table.Column<int>(type: "integer", nullable: false),
                    base_days = table.Column<int>(type: "integer", nullable: false),
                    home_days = table.Column<int>(type: "integer", nullable: false),
                    min_people_at_base = table.Column<int>(type: "integer", nullable: false),
                    min_rest_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    eligibility_threshold_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    leave_capacity = table.Column<int>(type: "integer", nullable: false),
                    leave_duration_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    emergency_freeze_active = table.Column<bool>(type: "boolean", nullable: false),
                    emergency_use_for_scheduling = table.Column<bool>(type: "boolean", nullable: false),
                    freeze_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pre_freeze_mode = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_home_leave_configs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_space_home_leave_configs_space_id",
                table: "space_home_leave_configs",
                column: "space_id",
                unique: true);

            // RLS policy for tenant isolation on space_home_leave_configs
            migrationBuilder.Sql(
                @"ALTER TABLE space_home_leave_configs ENABLE ROW LEVEL SECURITY;
                  CREATE POLICY tenant_isolation ON space_home_leave_configs
                  USING (space_id::text = current_setting('app.current_space_id', TRUE));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP POLICY IF EXISTS tenant_isolation ON space_home_leave_configs;
                  ALTER TABLE space_home_leave_configs DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "space_home_leave_configs");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "spaces");

            migrationBuilder.DropColumn(
                name: "management_timeout_minutes",
                table: "spaces");

            migrationBuilder.DropColumn(
                name: "permission_level",
                table: "space_memberships");

            migrationBuilder.DropColumn(
                name: "deleted_by_space_deletion",
                table: "groups");
        }
    }
}
