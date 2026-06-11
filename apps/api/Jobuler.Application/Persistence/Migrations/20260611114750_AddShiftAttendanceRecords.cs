using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftAttendanceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shift_attendance_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduling_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shift_attendance_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_shift_attendance_records_group_cycle_status",
                table: "shift_attendance_records",
                columns: new[] { "group_id", "scheduling_cycle_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_shift_attendance_records_person_cycle_status",
                table: "shift_attendance_records",
                columns: new[] { "person_id", "scheduling_cycle_id", "status" });

            migrationBuilder.CreateIndex(
                name: "idx_shift_attendance_records_shift_request",
                table: "shift_attendance_records",
                column: "shift_request_id",
                unique: true);

            migrationBuilder.Sql(
                @"ALTER TABLE shift_attendance_records ENABLE ROW LEVEL SECURITY;
                  CREATE POLICY tenant_isolation ON shift_attendance_records
                  USING (space_id::text = current_setting('app.current_space_id', TRUE));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"DROP POLICY IF EXISTS tenant_isolation ON shift_attendance_records;
                  ALTER TABLE shift_attendance_records DISABLE ROW LEVEL SECURITY;");

            migrationBuilder.DropTable(
                name: "shift_attendance_records");
        }
    }
}
