using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jobuler.Application.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpaceSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assignment_change_summaries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    baseline_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    added_count = table.Column<int>(type: "integer", nullable: false),
                    removed_count = table.Column<int>(type: "integer", nullable: false),
                    changed_count = table.Column<int>(type: "integer", nullable: false),
                    stability_score = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    diff_json = table.Column<string>(type: "jsonb", nullable: true),
                    computed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignment_change_summaries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_source = table.Column<string>(type: "text", nullable: false),
                    change_reason_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "text", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "availability_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_windows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "constraint_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<string>(type: "text", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    severity = table.Column<string>(type: "text", nullable: false),
                    rule_type = table.Column<string>(type: "text", nullable: false),
                    rule_payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_until = table.Column<DateOnly>(type: "date", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_constraint_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cumulative_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consecutive_hours_at_base = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    last_home_leave_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_assignments_7d = table.Column<int>(type: "integer", nullable: false),
                    total_assignments_14d = table.Column<int>(type: "integer", nullable: false),
                    total_assignments_30d = table.Column<int>(type: "integer", nullable: false),
                    total_assignments_90d = table.Column<int>(type: "integer", nullable: false),
                    total_assignments_period = table.Column<int>(type: "integer", nullable: false),
                    hard_tasks_7d = table.Column<int>(type: "integer", nullable: false),
                    hard_tasks_14d = table.Column<int>(type: "integer", nullable: false),
                    hard_tasks_30d = table.Column<int>(type: "integer", nullable: false),
                    hard_tasks_90d = table.Column<int>(type: "integer", nullable: false),
                    hard_tasks_period = table.Column<int>(type: "integer", nullable: false),
                    night_missions_7d = table.Column<int>(type: "integer", nullable: false),
                    night_missions_14d = table.Column<int>(type: "integer", nullable: false),
                    night_missions_30d = table.Column<int>(type: "integer", nullable: false),
                    night_missions_90d = table.Column<int>(type: "integer", nullable: false),
                    night_missions_period = table.Column<int>(type: "integer", nullable: false),
                    task_type_counts = table.Column<string>(type: "jsonb", nullable: true, defaultValue: "{}"),
                    total_hours_assigned_period = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cumulative_records", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    task_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    slot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shift_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    shift_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    burden_level = table.Column<string>(type: "text", nullable: true),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "double_shift_recommendations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    additional_slots_covered = table.Column<int>(type: "integer", nullable: false),
                    affected_date_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    affected_date_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_uncovered_slots_in_run = table.Column<int>(type: "integer", nullable: false),
                    dismissed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dismissed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cleared_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_double_shift_recommendations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fairness_counter_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_assignments = table.Column<int>(type: "integer", nullable: false),
                    hard_count = table.Column<int>(type: "integer", nullable: false),
                    normal_count = table.Column<int>(type: "integer", nullable: false),
                    easy_count = table.Column<int>(type: "integer", nullable: false),
                    burden_score = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fairness_counter_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fairness_counters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    as_of_date = table.Column<DateOnly>(type: "date", nullable: false),
                    total_assignments_7d = table.Column<int>(type: "integer", nullable: false),
                    total_assignments_14d = table.Column<int>(type: "integer", nullable: false),
                    total_assignments_30d = table.Column<int>(type: "integer", nullable: false),
                    hard_tasks_7d = table.Column<int>(type: "integer", nullable: false),
                    hard_tasks_14d = table.Column<int>(type: "integer", nullable: false),
                    hard_tasks_30d = table.Column<int>(type: "integer", nullable: false),
                    easy_tasks_7d = table.Column<int>(type: "integer", nullable: false),
                    easy_tasks_14d = table.Column<int>(type: "integer", nullable: false),
                    easy_tasks_30d = table.Column<int>(type: "integer", nullable: false),
                    burden_score_7d = table.Column<int>(type: "integer", nullable: false),
                    burden_score_14d = table.Column<int>(type: "integer", nullable: false),
                    burden_score_30d = table.Column<int>(type: "integer", nullable: false),
                    night_missions_7d = table.Column<int>(type: "integer", nullable: false),
                    consecutive_hard_count = table.Column<int>(type: "integer", nullable: false),
                    task_type_counts = table.Column<string>(type: "jsonb", nullable: true, defaultValue: "{}"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fairness_counters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "feedback_submissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feedback_submissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    opt_out_token = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "active"),
                    opted_out_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_invitations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_owner = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    home_leave_priority = table.Column<decimal>(type: "numeric(3,1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_memberships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    is_pinned = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_qualifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_qualifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    lemonsqueezy_subscription_id = table.Column<string>(type: "text", nullable: true),
                    lemonsqueezy_customer_id = table.Column<string>(type: "text", nullable: true),
                    trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    peak_member_count = table.Column<int>(type: "integer", nullable: false),
                    coupon_code = table.Column<string>(type: "text", nullable: true),
                    discount_percent = table.Column<int>(type: "integer", nullable: false),
                    canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "group_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "groups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    solver_horizon_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    solver_start_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    auto_publish = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_closed_base = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    min_rest_between_shifts_hours = table.Column<int>(type: "integer", nullable: false, defaultValue: 8),
                    join_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    template_type = table.Column<string>(type: "text", nullable: false, defaultValue: "Custom"),
                    allow_members_view_history = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    allow_members_view_stats = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    management_timeout_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    parent_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "home_leave_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_rest_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    eligibility_threshold_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    leave_capacity = table.Column<int>(type: "integer", nullable: false),
                    leave_duration_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    balance_value = table.Column<int>(type: "integer", nullable: false),
                    min_people_at_base = table.Column<int>(type: "integer", nullable: false),
                    mode = table.Column<string>(type: "text", nullable: false),
                    base_days = table.Column<int>(type: "integer", nullable: false),
                    home_days = table.Column<int>(type: "integer", nullable: false),
                    emergency_freeze_active = table.Column<bool>(type: "boolean", nullable: false),
                    emergency_use_for_scheduling = table.Column<bool>(type: "boolean", nullable: false),
                    freeze_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pre_freeze_mode = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_leave_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "home_leave_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    min_rest_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    eligibility_threshold_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    leave_capacity = table.Column<int>(type: "integer", nullable: false),
                    leave_duration_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_leave_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "member_qualifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    assigned_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_qualifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    deduplication_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ownership_transfer_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transferred_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    transferred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ownership_transfer_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pending_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact = table.Column<string>(type: "text", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    is_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_invitations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pending_ownership_transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_owner_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_owner_person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmation_token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_ownership_transfers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    full_name = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    profile_image_url = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    invitation_status = table.Column<string>(type: "text", nullable: true, defaultValue: "accepted"),
                    birthday = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_qualifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qualification = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_at = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_qualifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_restrictions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restriction_type = table.Column<string>(type: "text", nullable: false),
                    task_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_until = table.Column<DateOnly>(type: "date", nullable: true),
                    operational_note = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_restrictions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_role_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_role_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "push_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    p256dh = table.Column<string>(type: "text", nullable: false),
                    auth = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_push_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    trigger_type = table.Column<string>(type: "text", nullable: false),
                    baseline_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    solver_input_hash = table.Column<string>(type: "text", nullable: true),
                    progress_phase = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    result_summary_json = table.Column<string>(type: "jsonb", nullable: true),
                    error_summary = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    baseline_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rollback_source_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    summary_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_versions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sensitive_restriction_reasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    restriction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensitive_restriction_reasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "space_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_memberships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "space_permission_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_key = table.Column<string>(type: "text", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_permission_grants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "space_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permission_level = table.Column<string>(type: "text", nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "space_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    lemonsqueezy_subscription_id = table.Column<string>(type: "text", nullable: true),
                    lemonsqueezy_customer_id = table.Column<string>(type: "text", nullable: true),
                    trial_starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    trial_ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    current_period_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    current_period_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    peak_member_count = table.Column<int>(type: "integer", nullable: false),
                    canceled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    auto_renew = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "spaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    locale = table.Column<string>(type: "text", nullable: false),
                    invite_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "subscription_periods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscription_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    details_json = table.Column<string>(type: "jsonb", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_rotation_progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cycle_number = table.Column<int>(type: "integer", nullable: false),
                    completed_task_type_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    total_qualified_task_types = table.Column<int>(type: "integer", nullable: false),
                    completion_percentage = table.Column<double>(type: "double precision", nullable: false),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_rotation_progress", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    required_headcount = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    required_role_ids_json = table.Column<string>(type: "text", nullable: false),
                    required_qualification_ids_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<string>(type: "text", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_slots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_type_overlap_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_type_a_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_type_b_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overlap_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_type_overlap_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    burden_level = table.Column<string>(type: "text", nullable: false),
                    default_priority = table.Column<int>(type: "integer", nullable: false),
                    allows_overlap = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    shift_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    required_headcount = table.Column<int>(type: "integer", nullable: false),
                    burden_level = table.Column<string>(type: "text", nullable: false),
                    allows_double_shift = table.Column<bool>(type: "boolean", nullable: false),
                    allows_overlap = table.Column<bool>(type: "boolean", nullable: false),
                    daily_start_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    daily_end_time = table.Column<TimeSpan>(type: "interval", nullable: true),
                    qualification_requirements = table.Column<string>(type: "jsonb", nullable: false),
                    split_count = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unavailability_reasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unavailability_reasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_space_migrations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    migrated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    groups_migrated = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_space_migrations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    preferred_locale = table.Column<string>(type: "text", nullable: false),
                    profile_image_url = table.Column<string>(type: "text", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    is_platform_admin = table.Column<bool>(type: "boolean", nullable: false),
                    email_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    state_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    birthday = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_event_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_event_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "presence_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    space_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    is_derived = table.Column<bool>(type: "boolean", nullable: false),
                    unavailability_reason_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_presence_windows", x => x.id);
                    table.ForeignKey(
                        name: "FK_presence_windows_unavailability_reasons_unavailability_reas~",
                        column: x => x.unavailability_reason_id,
                        principalTable: "unavailability_reasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "email_verification_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_verification_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_email_verification_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webauthn_credentials",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credential_id = table.Column<byte[]>(type: "bytea", nullable: false),
                    public_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    sign_count = table.Column<long>(type: "bigint", nullable: false),
                    transports = table.Column<string[]>(type: "text[]", nullable: false),
                    nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_disabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webauthn_credentials", x => x.id);
                    table.ForeignKey(
                        name: "FK_webauthn_credentials_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assignments_schedule_version_id_task_slot_id_person_id",
                table: "assignments",
                columns: new[] { "schedule_version_id", "task_slot_id", "person_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_cumulative_records_lookup",
                table: "cumulative_records",
                columns: new[] { "space_id", "group_id", "period_id" });

            migrationBuilder.CreateIndex(
                name: "idx_cumulative_records_person",
                table: "cumulative_records",
                columns: new[] { "person_id", "period_id" });

            migrationBuilder.CreateIndex(
                name: "IX_cumulative_records_space_id_group_id_person_id_period_id",
                table: "cumulative_records",
                columns: new[] { "space_id", "group_id", "person_id", "period_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_daily_snapshots_date_range",
                table: "daily_snapshots",
                columns: new[] { "space_id", "group_id", "snapshot_date" });

            migrationBuilder.CreateIndex(
                name: "idx_daily_snapshots_period",
                table: "daily_snapshots",
                columns: new[] { "period_id", "snapshot_date" });

            migrationBuilder.CreateIndex(
                name: "idx_daily_snapshots_person",
                table: "daily_snapshots",
                columns: new[] { "person_id", "snapshot_date" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_snapshots_space_id_group_id_person_id_snapshot_date_s~",
                table: "daily_snapshots",
                columns: new[] { "space_id", "group_id", "person_id", "snapshot_date", "slot_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dsr_created_at",
                table: "double_shift_recommendations",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_dsr_space_group_status",
                table: "double_shift_recommendations",
                columns: new[] { "space_id", "group_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_dsr_space_run",
                table: "double_shift_recommendations",
                columns: new[] { "space_id", "schedule_run_id" });

            migrationBuilder.CreateIndex(
                name: "ix_dsr_space_task_status",
                table: "double_shift_recommendations",
                columns: new[] { "space_id", "group_task_id", "status" });

            migrationBuilder.CreateIndex(
                name: "uq_dsr_space_run_task",
                table: "double_shift_recommendations",
                columns: new[] { "space_id", "schedule_run_id", "group_task_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_token_hash",
                table: "email_verification_tokens",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "IX_email_verification_tokens_user_id",
                table: "email_verification_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_fcs_space_date",
                table: "fairness_counter_snapshots",
                columns: new[] { "space_id", "snapshot_date" });

            migrationBuilder.CreateIndex(
                name: "IX_fairness_counter_snapshots_space_id_person_id_snapshot_date",
                table: "fairness_counter_snapshots",
                columns: new[] { "space_id", "person_id", "snapshot_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fairness_counters_space_id_person_id_as_of_date",
                table: "fairness_counters",
                columns: new[] { "space_id", "person_id", "as_of_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_feedback_submissions_user_id_submitted_at_utc",
                table: "feedback_submissions",
                columns: new[] { "user_id", "submitted_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_group_invitations_opt_out_token",
                table: "group_invitations",
                column: "opt_out_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_memberships_group_id_person_id",
                table: "group_memberships",
                columns: new[] { "group_id", "person_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_types_space_id_name",
                table: "group_types",
                columns: new[] { "space_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_groups_join_code",
                table: "groups",
                column: "join_code",
                unique: true,
                filter: "join_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_groups_space_id_name",
                table: "groups",
                columns: new[] { "space_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_home_leave_configs_group_id",
                table: "home_leave_configs",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_home_leave_templates_space_id_name",
                table: "home_leave_templates",
                columns: new[] { "space_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_invitations_space_id_person_id",
                table: "pending_invitations",
                columns: new[] { "space_id", "person_id" });

            migrationBuilder.CreateIndex(
                name: "IX_pending_invitations_token_hash",
                table: "pending_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_ownership_transfers_confirmation_token",
                table: "pending_ownership_transfers",
                column: "confirmation_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_ownership_transfers_group_id",
                table: "pending_ownership_transfers",
                column: "group_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_settings_key",
                table: "platform_settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_presence_windows_unavailability_reason_id",
                table: "presence_windows",
                column: "unavailability_reason_id");

            migrationBuilder.CreateIndex(
                name: "ix_push_subscriptions_user_space",
                table: "push_subscriptions",
                columns: new[] { "user_id", "space_id" });

            migrationBuilder.CreateIndex(
                name: "uq_push_sub_user_space_endpoint",
                table: "push_subscriptions",
                columns: new[] { "user_id", "space_id", "endpoint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_schedule_versions_space_id_version_number",
                table: "schedule_versions",
                columns: new[] { "space_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_space_memberships_space_id_user_id",
                table: "space_memberships",
                columns: new[] { "space_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_space_permission_grants_space_id_user_id_permission_key",
                table: "space_permission_grants",
                columns: new[] { "space_id", "user_id", "permission_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_space_subscriptions_status",
                table: "space_subscriptions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "uq_space_subscriptions_space_id",
                table: "space_subscriptions",
                column: "space_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_spaces_invite_code",
                table: "spaces",
                column: "invite_code",
                unique: true,
                filter: "invite_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_subscription_periods_active",
                table: "subscription_periods",
                columns: new[] { "group_id", "status" },
                filter: "status = 'active'");

            migrationBuilder.CreateIndex(
                name: "idx_subscription_periods_group",
                table: "subscription_periods",
                columns: new[] { "space_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "idx_trp_group",
                table: "task_rotation_progress",
                column: "group_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_rotation_progress_space_id_person_id_group_id",
                table: "task_rotation_progress",
                columns: new[] { "space_id", "person_id", "group_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_type_overlap_rules_task_type_a_id_task_type_b_id",
                table: "task_type_overlap_rules",
                columns: new[] { "task_type_a_id", "task_type_b_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_types_space_id_name",
                table: "task_types",
                columns: new[] { "space_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_space_id_group_id_name",
                table: "tasks",
                columns: new[] { "space_id", "group_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_unavailability_reasons_space_id_is_active",
                table: "unavailability_reasons",
                columns: new[] { "space_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_user_space_migrations_user_id",
                table: "user_space_migrations",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webauthn_credentials_credential_id",
                table: "webauthn_credentials",
                column: "credential_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webauthn_credentials_user_id",
                table: "webauthn_credentials",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_event_logs_event_id",
                table: "webhook_event_logs",
                column: "event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_event_logs_processed_at",
                table: "webhook_event_logs",
                column: "processed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assignment_change_summaries");

            migrationBuilder.DropTable(
                name: "assignments");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "availability_windows");

            migrationBuilder.DropTable(
                name: "constraint_rules");

            migrationBuilder.DropTable(
                name: "cumulative_records");

            migrationBuilder.DropTable(
                name: "daily_snapshots");

            migrationBuilder.DropTable(
                name: "double_shift_recommendations");

            migrationBuilder.DropTable(
                name: "email_verification_tokens");

            migrationBuilder.DropTable(
                name: "fairness_counter_snapshots");

            migrationBuilder.DropTable(
                name: "fairness_counters");

            migrationBuilder.DropTable(
                name: "feedback_submissions");

            migrationBuilder.DropTable(
                name: "group_alerts");

            migrationBuilder.DropTable(
                name: "group_invitations");

            migrationBuilder.DropTable(
                name: "group_memberships");

            migrationBuilder.DropTable(
                name: "group_messages");

            migrationBuilder.DropTable(
                name: "group_qualifications");

            migrationBuilder.DropTable(
                name: "group_subscriptions");

            migrationBuilder.DropTable(
                name: "group_types");

            migrationBuilder.DropTable(
                name: "groups");

            migrationBuilder.DropTable(
                name: "home_leave_configs");

            migrationBuilder.DropTable(
                name: "home_leave_templates");

            migrationBuilder.DropTable(
                name: "member_qualifications");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "ownership_transfer_history");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropTable(
                name: "pending_invitations");

            migrationBuilder.DropTable(
                name: "pending_ownership_transfers");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "person_qualifications");

            migrationBuilder.DropTable(
                name: "person_restrictions");

            migrationBuilder.DropTable(
                name: "person_role_assignments");

            migrationBuilder.DropTable(
                name: "platform_settings");

            migrationBuilder.DropTable(
                name: "presence_windows");

            migrationBuilder.DropTable(
                name: "push_subscriptions");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "schedule_runs");

            migrationBuilder.DropTable(
                name: "schedule_versions");

            migrationBuilder.DropTable(
                name: "sensitive_restriction_reasons");

            migrationBuilder.DropTable(
                name: "space_memberships");

            migrationBuilder.DropTable(
                name: "space_permission_grants");

            migrationBuilder.DropTable(
                name: "space_roles");

            migrationBuilder.DropTable(
                name: "space_subscriptions");

            migrationBuilder.DropTable(
                name: "spaces");

            migrationBuilder.DropTable(
                name: "subscription_periods");

            migrationBuilder.DropTable(
                name: "system_logs");

            migrationBuilder.DropTable(
                name: "task_rotation_progress");

            migrationBuilder.DropTable(
                name: "task_slots");

            migrationBuilder.DropTable(
                name: "task_type_overlap_rules");

            migrationBuilder.DropTable(
                name: "task_types");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "user_space_migrations");

            migrationBuilder.DropTable(
                name: "webauthn_credentials");

            migrationBuilder.DropTable(
                name: "webhook_event_logs");

            migrationBuilder.DropTable(
                name: "unavailability_reasons");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
