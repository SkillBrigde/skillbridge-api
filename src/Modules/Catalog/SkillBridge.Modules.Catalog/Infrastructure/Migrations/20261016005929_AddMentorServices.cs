using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Catalog.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMentorServices : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mentor_services",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                service_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                duration_minutes = table.Column<int>(type: "integer", nullable: false),
                price = table.Column<decimal>(type: "numeric", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                total_sessions = table.Column<int>(type: "integer", nullable: false),
                deliverables = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mentor_services", x => x.id);
                table.CheckConstraint("CK_services_deleted", "NOT is_deleted OR NOT is_active");
                table.CheckConstraint("CK_services_duration", "duration_minutes BETWEEN 15 AND 240");
                table.CheckConstraint("CK_services_ids", "mentor_id <> '00000000-0000-0000-0000-000000000000' AND mentor_user_id <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("CK_services_price", "(currency = 'VND' AND price > 0 AND price <= 100000000 AND price = trunc(price)) OR (currency = 'USD' AND price > 0 AND price <= 5000 AND price = trunc(price, 2))");
                table.CheckConstraint("CK_services_sessions", "total_sessions BETWEEN 1 AND 52");
                table.CheckConstraint("CK_services_type", "service_type IN ('Tier1_ReviewCV', 'Tier2_MockInterview', 'Tier3_Roadmap')");
                table.CheckConstraint("CK_services_version", "version > 0");
            });

        migrationBuilder.CreateIndex(
            name: "IX_mentor_services_mentor_id_is_active_created_at_utc",
            schema: "catalog",
            table: "mentor_services",
            columns: new[] { "mentor_id", "is_active", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_mentor_services_mentor_user_id_created_at_utc",
            schema: "catalog",
            table: "mentor_services",
            columns: new[] { "mentor_user_id", "created_at_utc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "mentor_services",
            schema: "catalog");
    }
}
