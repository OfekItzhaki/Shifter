using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftAbsenceReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "late_cancellation_window_hours",
                table: "self_service_configs",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.AddColumn<int>(
                name: "max_late_cancellations_per_cycle",
                table: "self_service_configs",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateTable(
                name: "shift_absence_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduling_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_late = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    admin_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_absence_reports", x => x.id);
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
                name: "idx_shift_absence_reports_group_status",
                table: "shift_absence_reports",
                columns: new[] { "group_id", "status", "reported_at" });

            migrationBuilder.CreateIndex(
                name: "idx_shift_absence_reports_person_cycle",
                table: "shift_absence_reports",
                columns: new[] { "person_id", "scheduling_cycle_id", "is_late", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_shift_absence_reports_shift_request",
                table: "shift_absence_reports",
                column: "shift_request_id",
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

            migrationBuilder.DropTable(
                name: "shift_absence_reports");

            migrationBuilder.DropTable(
                name: "special_leave_requests");

            migrationBuilder.DropIndex(
                name: "IX_groups_parent_group_id",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "late_cancellation_window_hours",
                table: "self_service_configs");

            migrationBuilder.DropColumn(
                name: "max_late_cancellations_per_cycle",
                table: "self_service_configs");
        }
    }
}
