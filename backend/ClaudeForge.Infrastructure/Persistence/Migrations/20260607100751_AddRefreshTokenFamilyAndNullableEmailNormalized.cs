using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaudeForge.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenFamilyAndNullableEmailNormalized : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "email_normalized",
                table: "users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "refresh_tokens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "root_id",
                table: "refresh_tokens",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Backfill existing rows: root_id = id (each row is its own family root).
            migrationBuilder.Sql("UPDATE refresh_tokens SET root_id = id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "provider",
                table: "refresh_tokens");

            migrationBuilder.DropColumn(
                name: "root_id",
                table: "refresh_tokens");

            migrationBuilder.AlterColumn<string>(
                name: "email_normalized",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
