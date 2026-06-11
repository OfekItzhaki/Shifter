using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organization_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    billing_mode = table.Column<string>(type: "text", nullable: false),
                    tier_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    provider_subscription_id = table.Column<string>(type: "text", nullable: true),
                    provider_customer_id = table.Column<string>(type: "text", nullable: true),
                    current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    covered_space_limit = table.Column<int>(type: "integer", nullable: true),
                    covered_member_limit = table.Column<int>(type: "integer", nullable: true),
                    auto_renew = table.Column<bool>(type: "boolean", nullable: false),
                    canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_subscriptions_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_organization_subscriptions_status",
                table: "organization_subscriptions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_organization_subscriptions_organization_id",
                table: "organization_subscriptions",
                column: "organization_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_subscriptions");
        }
    }
}
