using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class CompleteIdentitySecurity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<bool>(
            name: "IsActive",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: true,
            oldClrType: typeof(bool),
            oldType: "boolean");

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
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsEmailConfirmed",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsPhoneConfirmed",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "LockoutEnabled",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LockoutEndUtc",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NormalizedEmail",
            schema: "identity",
            table: "users",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "PasswordHash",
            schema: "identity",
            table: "users",
            type: "character varying(500)",
            maxLength: 500,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "PhoneNumber",
            schema: "identity",
            table: "users",
            type: "character varying(30)",
            maxLength: 30,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SecurityStamp",
            schema: "identity",
            table: "users",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<bool>(
            name: "TwoFactorEnabled",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "UpdatedAtUtc",
            schema: "identity",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        // The original users table had no credentials. Preserve its rows without inventing passwords.
        // Normalized-email collisions deliberately fail the unique index below for manual reconciliation.
        migrationBuilder.Sql("""
            UPDATE identity.users
            SET "NormalizedEmail" = upper(btrim("Email")),
                "SecurityStamp" = replace("Id"::text, '-', ''),
                "IsActive" = FALSE;
            """);

        migrationBuilder.CreateTable(
            name: "external_logins",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ProviderKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProviderDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_external_logins", x => x.Id);
                table.ForeignKey(
                    name: "FK_external_logins_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

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
            name: "refresh_tokens",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                SecurityStamp = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ReplacedByToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedByIp = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_refresh_tokens_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "roles",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                NormalizedName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_roles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "user_roles",
            schema: "identity",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_user_roles_roles_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_user_roles_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "identity",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_users_NormalizedEmail",
            schema: "identity",
            table: "users",
            column: "NormalizedEmail",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_external_logins_Provider_ProviderKey",
            schema: "identity",
            table: "external_logins",
            columns: new[] { "Provider", "ProviderKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_external_logins_UserId",
            schema: "identity",
            table: "external_logins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_ProcessedOnUtc",
            schema: "identity",
            table: "outbox_messages",
            column: "ProcessedOnUtc");

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_Token",
            schema: "identity",
            table: "refresh_tokens",
            column: "Token",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_refresh_tokens_UserId",
            schema: "identity",
            table: "refresh_tokens",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_roles_Name",
            schema: "identity",
            table: "roles",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_roles_NormalizedName",
            schema: "identity",
            table: "roles",
            column: "NormalizedName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_roles_RoleId",
            schema: "identity",
            table: "user_roles",
            column: "RoleId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "external_logins",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "refresh_tokens",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "user_roles",
            schema: "identity");

        migrationBuilder.DropTable(
            name: "roles",
            schema: "identity");

        migrationBuilder.DropIndex(
            name: "IX_users_NormalizedEmail",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "AccessFailedCount",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "AvatarUrl",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "IsEmailConfirmed",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "IsPhoneConfirmed",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "LockoutEnabled",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "LockoutEndUtc",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "NormalizedEmail",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "PasswordHash",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "PhoneNumber",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "SecurityStamp",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "TwoFactorEnabled",
            schema: "identity",
            table: "users");

        migrationBuilder.DropColumn(
            name: "UpdatedAtUtc",
            schema: "identity",
            table: "users");

        migrationBuilder.AlterColumn<bool>(
            name: "IsActive",
            schema: "identity",
            table: "users",
            type: "boolean",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "boolean",
            oldDefaultValue: true);
    }
}
