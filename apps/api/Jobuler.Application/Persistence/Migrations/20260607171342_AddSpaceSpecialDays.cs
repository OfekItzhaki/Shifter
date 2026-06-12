using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaceSpecialDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "space_special_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    home_leave_weight_multiplier = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    requires_coverage = table.Column<bool>(type: "boolean", nullable: false),
                    is_auto_generated = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_special_days", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "special_leave_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    admin_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    presence_window_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_special_leave_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_special_leave_requests_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_special_leave_requests_presence_windows_presence_window_id",
                        column: x => x.presence_window_id,
                        principalTable: "presence_windows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_groups_parent_group_id",
                table: "groups",
                column: "parent_group_id");

            migrationBuilder.CreateIndex(
                name: "IX_space_special_days_space_date_name",
                table: "space_special_days",
                columns: new[] { "space_id", "date", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_special_leave_requests_person_status_start",
                table: "special_leave_requests",
                columns: new[] { "person_id", "status", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "idx_special_leave_requests_space_status_start",
                table: "special_leave_requests",
                columns: new[] { "space_id", "status", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "IX_special_leave_requests_presence_window_id",
                table: "special_leave_requests",
                column: "presence_window_id");

            migrationBuilder.Sql(
                @"ALTER TABLE space_special_days ENABLE ROW LEVEL SECURITY;
                  CREATE POLICY tenant_isolation ON space_special_days
                  USING (space_id::text = current_setting('app.current_space_id', TRUE));");

            migrationBuilder.Sql(
                @"ALTER TABLE special_leave_requests ENABLE ROW LEVEL SECURITY;
                  CREATE POLICY tenant_isolation ON special_leave_requests
                  USING (space_id::text = current_setting('app.current_space_id', TRUE));");

            migrationBuilder.AddForeignKey(
                name: "FK_groups_groups_parent_group_id",
                table: "groups",
                column: "parent_group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_groups_groups_parent_group_id",
                table: "groups");

            migrationBuilder.Sql(
                @"DROP POLICY IF EXISTS tenant_isolation ON special_leave_requests;
                  ALTER TABLE special_leave_requests DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.Sql(
                @"DROP POLICY IF EXISTS tenant_isolation ON space_special_days;
                  ALTER TABLE space_special_days DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "space_special_days");

            migrationBuilder.DropTable(
                name: "special_leave_requests");

            migrationBuilder.DropIndex(
                name: "IX_groups_parent_group_id",
                table: "groups");
        }
    }
}
