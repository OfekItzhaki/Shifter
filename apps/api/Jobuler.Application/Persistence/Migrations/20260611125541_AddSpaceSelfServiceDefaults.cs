using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaceSelfServiceDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "space_self_service_defaults",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_shifts_per_cycle = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_shifts_per_cycle = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    request_window_open_offset_hours = table.Column<int>(type: "integer", nullable: false, defaultValue: 168),
                    request_window_close_offset_hours = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    cancellation_cutoff_hours = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    max_absences_per_cycle = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    max_late_cancellations_per_cycle = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    late_cancellation_window_hours = table.Column<int>(type: "integer", nullable: false, defaultValue: 24),
                    waitlist_offer_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    cycle_duration_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    allow_member_shift_claims = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    allow_waitlist = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    allow_shift_change_requests = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    allow_absence_reports = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    allow_shift_swaps = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_self_service_defaults", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_space_self_service_defaults_space_id",
                table: "space_self_service_defaults",
                column: "space_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "space_self_service_defaults");
        }
    }
}
