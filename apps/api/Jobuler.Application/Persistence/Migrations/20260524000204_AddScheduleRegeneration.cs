using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleRegeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "schedule_versions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "supersedes_version_id",
                table: "schedule_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "schedule_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "result_version_id",
                table: "schedule_runs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_schedule_versions_supersedes_version_id",
                table: "schedule_versions",
                column: "supersedes_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_runs_group_id",
                table: "schedule_runs",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_runs_group_regeneration",
                table: "schedule_runs",
                columns: new[] { "space_id", "group_id", "status" },
                filter: "trigger_type = 'regeneration' AND status IN ('queued', 'running')");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_runs_result_version_id",
                table: "schedule_runs",
                column: "result_version_id");

            migrationBuilder.AddForeignKey(
                name: "FK_schedule_runs_groups_group_id",
                table: "schedule_runs",
                column: "group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_schedule_runs_schedule_versions_result_version_id",
                table: "schedule_runs",
                column: "result_version_id",
                principalTable: "schedule_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_schedule_versions_schedule_versions_supersedes_version_id",
                table: "schedule_versions",
                column: "supersedes_version_id",
                principalTable: "schedule_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_schedule_runs_groups_group_id",
                table: "schedule_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_schedule_runs_schedule_versions_result_version_id",
                table: "schedule_runs");

            migrationBuilder.DropForeignKey(
                name: "FK_schedule_versions_schedule_versions_supersedes_version_id",
                table: "schedule_versions");

            migrationBuilder.DropIndex(
                name: "IX_schedule_versions_supersedes_version_id",
                table: "schedule_versions");

            migrationBuilder.DropIndex(
                name: "IX_schedule_runs_group_id",
                table: "schedule_runs");

            migrationBuilder.DropIndex(
                name: "ix_schedule_runs_group_regeneration",
                table: "schedule_runs");

            migrationBuilder.DropIndex(
                name: "IX_schedule_runs_result_version_id",
                table: "schedule_runs");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "schedule_versions");

            migrationBuilder.DropColumn(
                name: "supersedes_version_id",
                table: "schedule_versions");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "schedule_runs");

            migrationBuilder.DropColumn(
                name: "result_version_id",
                table: "schedule_runs");
        }
    }
}
