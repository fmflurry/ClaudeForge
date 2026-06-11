using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddControlCenterSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analysis_config",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    static_weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0.6m),
                    dynamic_weight = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0.4m),
                    pass_threshold = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 80m),
                    fail_threshold = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 50m),
                    max_workers = table.Column<int>(type: "integer", nullable: false, defaultValue: 2),
                    retry_limit = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    analysis_timeout_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 300),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analysis_config", x => x.id);
                    table.CheckConstraint("chk_analysis_config_thresholds", "pass_threshold > fail_threshold");
                    table.CheckConstraint("chk_analysis_config_weights", "static_weight + dynamic_weight = 1.0");
                });

            migrationBuilder.CreateTable(
                name: "config_change_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_config = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    new_config = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    change_description = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_config_change_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_notifications_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "org_plugin_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_by = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_plugin_blocks", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_plugin_blocks_organizations_org_id",
                        column: x => x.org_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_plugin_blocks_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_alerts = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    in_app_alerts = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notification_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_notification_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_config_change_log_created_at",
                table: "config_change_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_user",
                table: "notifications",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_notifications_user_unread",
                table: "notifications",
                columns: new[] { "user_id", "is_read" });

            migrationBuilder.CreateIndex(
                name: "ix_org_plugin_blocks_org_plugin",
                table: "org_plugin_blocks",
                columns: new[] { "org_id", "plugin_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_org_plugin_blocks_plugin_id",
                table: "org_plugin_blocks",
                column: "plugin_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analysis_config");

            migrationBuilder.DropTable(
                name: "config_change_log");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "org_plugin_blocks");

            migrationBuilder.DropTable(
                name: "user_notification_preferences");
        }
    }
}
