using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftChangeRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shift_change_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduling_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_shift_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_shift_slot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    admin_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_change_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_shift_change_requests_group_status",
                table: "shift_change_requests",
                columns: new[] { "group_id", "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "idx_shift_change_requests_person_cycle",
                table: "shift_change_requests",
                columns: new[] { "person_id", "scheduling_cycle_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_shift_change_requests_requested_slot",
                table: "shift_change_requests",
                column: "requested_shift_slot_id");

            migrationBuilder.CreateIndex(
                name: "idx_shift_change_requests_shift_request_pending",
                table: "shift_change_requests",
                column: "shift_request_id",
                filter: "status = 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shift_change_requests");
        }
    }
}
