using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSelfServiceWorkflowToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ends_at",
                table: "shift_slots",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "starts_at",
                table: "shift_slots",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE shift_slots
                SET
                    starts_at = ((date + start_time) AT TIME ZONE 'UTC'),
                    ends_at = ((date + end_time + CASE WHEN end_time <= start_time THEN INTERVAL '1 day' ELSE INTERVAL '0 day' END) AT TIME ZONE 'UTC')
                """);

            migrationBuilder.AddColumn<bool>(
                name: "allow_absence_reports",
                table: "self_service_configs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_member_shift_claims",
                table: "self_service_configs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_shift_change_requests",
                table: "self_service_configs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_shift_swaps",
                table: "self_service_configs",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "allow_waitlist",
                table: "self_service_configs",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ends_at",
                table: "shift_slots");

            migrationBuilder.DropColumn(
                name: "starts_at",
                table: "shift_slots");

            migrationBuilder.DropColumn(
                name: "allow_absence_reports",
                table: "self_service_configs");

            migrationBuilder.DropColumn(
                name: "allow_member_shift_claims",
                table: "self_service_configs");

            migrationBuilder.DropColumn(
                name: "allow_shift_change_requests",
                table: "self_service_configs");

            migrationBuilder.DropColumn(
                name: "allow_shift_swaps",
                table: "self_service_configs");

            migrationBuilder.DropColumn(
                name: "allow_waitlist",
                table: "self_service_configs");
        }
    }
}
