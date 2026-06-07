using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_organization_members",
                table: "organization_members");

            migrationBuilder.AddColumn<Guid>(
                name: "id",
                table: "organization_members",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_organization_members",
                table: "organization_members",
                column: "id");

            migrationBuilder.AddCheckConstraint(
                name: "chk_refresh_tokens_token_hash_length",
                table: "refresh_tokens",
                sql: "char_length(trim(token_hash)) = 64");

            migrationBuilder.CreateIndex(
                name: "ix_organization_members_org_user",
                table: "organization_members",
                columns: new[] { "org_id", "user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_refresh_tokens_token_hash_length",
                table: "refresh_tokens");

            migrationBuilder.DropPrimaryKey(
                name: "PK_organization_members",
                table: "organization_members");

            migrationBuilder.DropIndex(
                name: "ix_organization_members_org_user",
                table: "organization_members");

            migrationBuilder.DropColumn(
                name: "id",
                table: "organization_members");

            migrationBuilder.AddPrimaryKey(
                name: "PK_organization_members",
                table: "organization_members",
                columns: new[] { "org_id", "user_id" });
        }
    }
}
