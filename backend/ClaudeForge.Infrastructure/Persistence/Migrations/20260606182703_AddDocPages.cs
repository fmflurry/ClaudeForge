using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocPages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doc_pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    slug = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    content_markdown = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    last_updated = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    search_vector = table.Column<string>(type: "tsvector", nullable: true,
                        computedColumnSql: "setweight(to_tsvector('english', coalesce(title,'')), 'A') || setweight(to_tsvector('english', coalesce(content_markdown,'')), 'B')",
                        stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_pages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_doc_pages_slug",
                table: "doc_pages",
                column: "slug",
                unique: true);

            migrationBuilder.Sql(
                "CREATE INDEX IF NOT EXISTS idx_doc_pages_search_vector ON doc_pages USING GIN (search_vector);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doc_pages");
        }
    }
}
