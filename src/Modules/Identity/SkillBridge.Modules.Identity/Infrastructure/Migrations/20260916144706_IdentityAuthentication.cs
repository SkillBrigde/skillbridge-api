using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class IdentityAuthentication : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AccessFailedCount",
            schema: "identity",
            table: "users",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "AvatarUrl",
            schema: "identity",
            table: "users",
            type: "character varying(2048)",
            maxLength: 2048,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsEmailVerified",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LockoutEndUtc",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PasswordHash",
            schema: "identity",
            table: "users",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<string[]>(
            name: "Roles",
            schema: "identity",
            table: "users",
            type: "text[]",
            nullable: false,
            defaultValue: new string[0]);

        migrationBuilder.AddColumn<Guid>(
            name: "SecurityStamp",
            schema: "identity",
            table: "users",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "UpdatedAtUtc",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE identity.users
            SET "Roles" = ARRAY["Role"], "SecurityStamp" = gen_random_uuid();
            """);

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Content = table.Column<string>(type: "jsonb", nullable: false),
                OccurredOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProcessedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "security_audit_entries",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_security_audit_entries", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "user_sessions",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastActiveAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_sessions_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_refresh_tokens_user_sessions_SessionId",
                    column: x => x.SessionId,
                    principalSchema: "identity",
                    principalTable: "user_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_OccurredOnUtc",
            schema: "identity",
            table: "outbox_messages",
            column: "OccurredOnUtc",
            filter: "\"ProcessedOnUtc\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_SessionId",
            schema: "identity",
            table: "refresh_tokens",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_TokenHash",
            schema: "identity",
            table: "refresh_tokens",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_security_audit_entries_SubjectId_CreatedAtUtc",
            schema: "identity",
            table: "security_audit_entries",
            columns: new[] { "SubjectId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_user_sessions_UserId_IsActive",
            schema: "identity",
            table: "user_sessions",
            columns: new[] { "UserId", "IsActive" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "refresh_tokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "security_audit_entries",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "user_sessions",
            schema: "identity");

        migrationBuilder.DropColumn(
            name: "AccessFailedCount",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "AvatarUrl",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "IsEmailVerified",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "LockoutEndUtc",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "PasswordHash",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "Roles",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "SecurityStamp",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "UpdatedAtUtc",
            schema: "identity",
            table: "users");
    }
}
