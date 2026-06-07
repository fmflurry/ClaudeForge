using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenRootIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_root_id",
                table: "refresh_tokens",
                column: "root_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_refresh_tokens_root_id",
                table: "refresh_tokens");
        }
    }
}
