using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace ClaudeForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMarketplaceSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    dimension = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plugins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_normalized = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "text", nullable: false),
                    download_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('english', coalesce(name,'') || ' ' || coalesce(description,''))", stored: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plugin_categories",
                columns: table => new
                {
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_categories", x => new { x.plugin_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_plugin_categories_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_plugin_categories_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plugin_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    version_sort = table.Column<long>(type: "bigint", nullable: false),
                    release_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    is_latest = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    package_key = table.Column<string>(type: "text", nullable: false),
                    package_format = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    download_count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    readme_text = table.Column<string>(type: "text", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugin_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_plugin_versions_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_aggregates",
                columns: table => new
                {
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    window_start = table.Column<DateOnly>(type: "date", nullable: false),
                    count = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_aggregates", x => new { x.plugin_id, x.version, x.event_type, x.window_start });
                    table.ForeignKey(
                        name: "FK_telemetry_aggregates_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "telemetry_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    plugin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<string>(type: "text", nullable: true),
                    anon_client_id = table.Column<string>(type: "char(64)", nullable: true),
                    client_os = table.Column<string>(type: "text", nullable: true),
                    client_arch = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telemetry_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_telemetry_events_plugins_plugin_id",
                        column: x => x.plugin_id,
                        principalTable: "plugins",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_categories_dimension_value",
                table: "categories",
                columns: new[] { "dimension", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_plugin_categories_category_id",
                table: "plugin_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "idx_plugin_versions_sort",
                table: "plugin_versions",
                columns: new[] { "plugin_id", "version_sort" });

            migrationBuilder.CreateIndex(
                name: "ix_plugin_versions_plugin_version",
                table: "plugin_versions",
                columns: new[] { "plugin_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plugin_versions_single_latest",
                table: "plugin_versions",
                column: "plugin_id",
                unique: true,
                filter: "is_latest = TRUE");

            migrationBuilder.CreateIndex(
                name: "ix_plugins_name_normalized",
                table: "plugins",
                column: "name_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_plugins_slug",
                table: "plugins",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_telemetry_events_plugin",
                table: "telemetry_events",
                column: "plugin_id");

            migrationBuilder.CreateIndex(
                name: "idx_telemetry_events_ts",
                table: "telemetry_events",
                column: "occurred_at");

            // GIN index on the tsvector generated column for full-text search performance
            migrationBuilder.Sql(
                "CREATE INDEX idx_plugins_search_vector ON plugins USING GIN(search_vector);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_plugins_search_vector;");

            migrationBuilder.DropTable(
                name: "plugin_categories");

            migrationBuilder.DropTable(
                name: "plugin_versions");

            migrationBuilder.DropTable(
                name: "telemetry_aggregates");

            migrationBuilder.DropTable(
                name: "telemetry_events");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "plugins");
        }
    }
}
