using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationsForPortability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    primary_owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    setup_template = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    default_locale = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    default_timezone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    relocated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    disabled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    purge_eligible_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dedicated_deployment_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "spaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                INSERT INTO organizations (
                    id,
                    display_name,
                    normalized_name,
                    primary_owner_user_id,
                    country_code,
                    setup_template,
                    default_locale,
                    status,
                    created_at,
                    updated_at
                )
                SELECT
                    s.id,
                    CASE
                        WHEN u.country_code IS NOT NULL AND btrim(u.country_code) <> ''
                            THEN upper(btrim(u.country_code)) || ' General'
                        ELSE s.name
                    END,
                    upper(CASE
                        WHEN u.country_code IS NOT NULL AND btrim(u.country_code) <> ''
                            THEN upper(btrim(u.country_code)) || ' General'
                        ELSE s.name
                    END),
                    s.owner_user_id,
                    upper(nullif(btrim(u.country_code), '')),
                    'general',
                    s.locale,
                    'Active',
                    s.created_at,
                    s.updated_at
                FROM spaces s
                LEFT JOIN users u ON u.id = s.owner_user_id
                WHERE NOT EXISTS (
                    SELECT 1 FROM organizations o WHERE o.id = s.id
                );
                """);

            migrationBuilder.Sql("""
                UPDATE spaces
                SET organization_id = id
                WHERE organization_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "organization_id",
                table: "spaces",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_spaces_organization_id",
                table: "spaces",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_groups_parent_group_id",
                table: "groups",
                column: "parent_group_id");

            migrationBuilder.CreateIndex(
                name: "idx_organizations_country_template",
                table: "organizations",
                columns: new[] { "country_code", "setup_template" });

            migrationBuilder.CreateIndex(
                name: "idx_organizations_normalized_name",
                table: "organizations",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "idx_organizations_primary_owner_user_id",
                table: "organizations",
                column: "primary_owner_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_organizations_purge_eligible_at",
                table: "organizations",
                column: "purge_eligible_at");

            migrationBuilder.CreateIndex(
                name: "idx_organizations_status",
                table: "organizations",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_groups_groups_parent_group_id",
                table: "groups",
                column: "parent_group_id",
                principalTable: "groups",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_spaces_organizations_organization_id",
                table: "spaces",
                column: "organization_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_groups_groups_parent_group_id",
                table: "groups");

            migrationBuilder.DropForeignKey(
                name: "FK_spaces_organizations_organization_id",
                table: "spaces");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropIndex(
                name: "idx_spaces_organization_id",
                table: "spaces");

            migrationBuilder.DropIndex(
                name: "IX_groups_parent_group_id",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "organization_id",
                table: "spaces");
        }
    }
}
