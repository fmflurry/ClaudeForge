using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityAnalysisSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "security_score",
                table: "plugins",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "security_status",
                table: "plugins",
                type: "text",
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.CreateTable(
                name: "analysis_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plugin_version = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "queued"),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_jobs", x => x.id);
                    table.CheckConstraint("chk_analysis_jobs_status", "status IN ('queued', 'processing', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_analysis_jobs_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "analysis_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plugin_version = table.Column<string>(type: "text", nullable: false),
                    static_eslint_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    static_semgrep_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    static_gitleaks_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    static_trivy_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    static_findings = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    dynamic_behavior_score = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    dynamic_findings = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    total_score = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    analysis_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    static_weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0.6m),
                    dynamic_weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0.4m),
                    pass_threshold = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 80m),
                    fail_threshold = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 50m),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_results", x => x.id);
                    table.CheckConstraint("chk_analysis_results_status", "status IN ('passed', 'failed', 'in_review')");
                    table.ForeignKey(
                        name: "FK_analysis_results_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "author_reputation",
                columns: table => new
                {
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    karma_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    level = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    badges = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_author_reputation", x => x.author_id);
                });

            migrationBuilder.CreateTable(
                name: "badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    icon_url = table.Column<string>(type: "text", nullable: true),
                    requirements = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leaderboard_cache",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    karma_points = table.Column<int>(type: "integer", nullable: false),
                    badge_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    period = table.Column<string>(type: "text", nullable: false, defaultValue: "all_time"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: true),
                    calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaderboard_cache", x => x.id);
                    table.CheckConstraint("chk_leaderboard_cache_period", "period IN ('weekly', 'monthly', 'all_time')");
                });

            migrationBuilder.CreateTable(
                name: "safe_zone_plugins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plugin_version = table.Column<string>(type: "text", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_safe_zone_plugins", x => x.id);
                    table.ForeignKey(
                        name: "FK_safe_zone_plugins_organizations_org_id",
                        column: x => x.org_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_safe_zone_plugins_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appeals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_result_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    evidence = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appeals", x => x.id);
                    table.CheckConstraint("chk_appeals_status", "status IN ('pending', 'approved', 'rejected')");
                    table.ForeignKey(
                        name: "FK_appeals_analysis_results_analysis_result_id",
                        column: x => x.analysis_result_id,
                        principalTable: "analysis_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_appeals_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "karma_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_karma_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_karma_events_author_reputation_author_id",
                        column: x => x.author_id,
                        principalTable: "author_reputation",
                        principalColumn: "author_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "author_badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    badge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    awarded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_author_badges", x => x.id);
                    table.ForeignKey(
                        name: "FK_author_badges_badges_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "chk_plugins_security_status",
                table: "plugins",
                sql: "security_status IN ('pending', 'passed', 'failed', 'in_review')");

            migrationBuilder.CreateIndex(
                name: "idx_analysis_jobs_priority",
                table: "analysis_jobs",
                columns: new[] { "priority", "created_at" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "idx_analysis_jobs_status",
                table: "analysis_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_analysis_jobs_plugin_id",
                table: "analysis_jobs",
                column: "plugin_id");

            migrationBuilder.CreateIndex(
                name: "idx_analysis_results_plugin",
                table: "analysis_results",
                column: "plugin_id");

            migrationBuilder.CreateIndex(
                name: "idx_analysis_results_status",
                table: "analysis_results",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_appeals_plugin",
                table: "appeals",
                column: "plugin_id");

            migrationBuilder.CreateIndex(
                name: "idx_appeals_status",
                table: "appeals",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_appeals_analysis_result_id",
                table: "appeals",
                column: "analysis_result_id");

            migrationBuilder.CreateIndex(
                name: "ix_author_badges_author_badge",
                table: "author_badges",
                columns: new[] { "author_id", "badge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_author_badges_badge_id",
                table: "author_badges",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "ix_badges_slug",
                table: "badges",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_karma_events_author_id",
                table: "karma_events",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "idx_leaderboard_cache_period_org_rank",
                table: "leaderboard_cache",
                columns: new[] { "period", "org_id", "rank" });

            migrationBuilder.CreateIndex(
                name: "idx_safe_zone_org",
                table: "safe_zone_plugins",
                column: "org_id");

            migrationBuilder.CreateIndex(
                name: "idx_safe_zone_plugin",
                table: "safe_zone_plugins",
                column: "plugin_id");

            migrationBuilder.CreateIndex(
                name: "ix_safe_zone_plugins_org_plugin_version",
                table: "safe_zone_plugins",
                columns: new[] { "org_id", "plugin_id", "plugin_version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_jobs");

            migrationBuilder.DropTable(
                name: "appeals");

            migrationBuilder.DropTable(
                name: "author_badges");

            migrationBuilder.DropTable(
                name: "karma_events");

            migrationBuilder.DropTable(
                name: "leaderboard_cache");

            migrationBuilder.DropTable(
                name: "safe_zone_plugins");

            migrationBuilder.DropTable(
                name: "analysis_results");

            migrationBuilder.DropTable(
                name: "badges");

            migrationBuilder.DropTable(
                name: "author_reputation");

            migrationBuilder.DropCheckConstraint(
                name: "chk_plugins_security_status",
                table: "plugins");

            migrationBuilder.DropColumn(
                name: "security_score",
                table: "plugins");

            migrationBuilder.DropColumn(
                name: "security_status",
                table: "plugins");
        }
    }
}
