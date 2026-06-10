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
            // ── users ────────────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "text", nullable: false),
                    email_normalized = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_email_normalized",
                table: "users",
                column: "email_normalized",
                unique: true);

            // ── user_identities ──────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "user_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_identities", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_identities_user_id",
                table: "user_identities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_identities_provider_subject",
                table: "user_identities",
                columns: new[] { "provider", "subject" },
                unique: true);

            // ── organizations ────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_normalized = table.Column<string>(type: "text", nullable: false),
                    slug = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                    table.ForeignKey(
                        name: "FK_organizations_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_created_by",
                table: "organizations",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_organizations_name_normalized",
                table: "organizations",
                column: "name_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organizations_slug",
                table: "organizations",
                column: "slug",
                unique: true);

            // ── organization_members ─────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "organization_members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_members_organizations_org_id",
                        column: x => x.org_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_members_user_id",
                table: "organization_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_organization_members_org_user",
                table: "organization_members",
                columns: new[] { "org_id", "user_id" },
                unique: true);

            // ── organization_invitations ─────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "organization_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_normalized = table.Column<string>(type: "text", nullable: false),
                    invited_by = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false, defaultValue: "member"),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "pending"),
                    token = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_organization_invitations_organizations_org_id",
                        column: x => x.org_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_organization_invitations_users_invited_by",
                        column: x => x.invited_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_organization_invitations_invited_by",
                table: "organization_invitations",
                column: "invited_by");

            migrationBuilder.CreateIndex(
                name: "ix_organization_invitations_token",
                table: "organization_invitations",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_organization_invitations_pending_org_email",
                table: "organization_invitations",
                columns: new[] { "org_id", "email_normalized" },
                unique: true,
                filter: "status = 'pending'");

            // ── org_audit_log ────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "org_audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_org_audit_log_organizations_org_id",
                        column: x => x.org_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_org_audit_log_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_org_audit_log_actor_user_id",
                table: "org_audit_log",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_org_audit_log_org_id",
                table: "org_audit_log",
                column: "org_id");

            // ── refresh_tokens ───────────────────────────────────────────────────────
            // Note: provider and root_id columns are added later in
            // AddRefreshTokenFamilyAndNullableEmailNormalized. This CreateTable reflects
            // the state described by the AddAuthSchema Designer snapshot.
            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "char(64)", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rotated_to = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.id);
                    table.CheckConstraint("chk_refresh_tokens_token_hash_length", "char_length(trim(token_hash)) = 64");
                    table.ForeignKey(
                        name: "FK_refresh_tokens_refresh_tokens_rotated_to",
                        column: x => x.rotated_to,
                        principalTable: "refresh_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_rotated_to",
                table: "refresh_tokens",
                column: "rotated_to");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            // ── plugins: add auth columns ────────────────────────────────────────────
            // Add visibility, owner_org_id, owner_user_id columns that depend on users
            // and organizations tables now being present.
            migrationBuilder.AddColumn<string>(
                name: "visibility",
                table: "plugins",
                type: "text",
                nullable: false,
                defaultValue: "public");

            migrationBuilder.AddColumn<Guid>(
                name: "owner_org_id",
                table: "plugins",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "plugins",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_visibility_owner",
                table: "plugins",
                sql: "visibility = 'public' OR owner_org_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_plugins_owner_org_id",
                table: "plugins",
                column: "owner_org_id");

            migrationBuilder.CreateIndex(
                name: "IX_plugins_owner_user_id",
                table: "plugins",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "idx_plugins_visibility_org",
                table: "plugins",
                columns: new[] { "visibility", "owner_org_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_plugins_organizations_owner_org_id",
                table: "plugins",
                column: "owner_org_id",
                principalTable: "organizations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_plugins_users_owner_user_id",
                table: "plugins",
                column: "owner_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_plugins_organizations_owner_org_id",
                table: "plugins");

            migrationBuilder.DropForeignKey(
                name: "FK_plugins_users_owner_user_id",
                table: "plugins");

            migrationBuilder.DropIndex(
                name: "idx_plugins_visibility_org",
                table: "plugins");

            migrationBuilder.DropIndex(
                name: "IX_plugins_owner_org_id",
                table: "plugins");

            migrationBuilder.DropIndex(
                name: "IX_plugins_owner_user_id",
                table: "plugins");

            migrationBuilder.DropCheckConstraint(
                name: "chk_visibility_owner",
                table: "plugins");

            migrationBuilder.DropColumn(
                name: "visibility",
                table: "plugins");

            migrationBuilder.DropColumn(
                name: "owner_org_id",
                table: "plugins");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "plugins");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "org_audit_log");

            migrationBuilder.DropTable(
                name: "organization_invitations");

            migrationBuilder.DropTable(
                name: "organization_members");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "user_identities");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
