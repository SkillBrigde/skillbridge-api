using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Profiles.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Initial_Profiles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "profiles");

        migrationBuilder.CreateTable(
            name: "mentee_profiles",
            schema: "profiles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                career_goal = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                current_level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                budget_min = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                budget_max = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mentee_profiles", x => x.id);
                table.CheckConstraint("ck_mentee_profiles_budget", "budget_min >= 0 AND budget_max >= budget_min");
                table.CheckConstraint("ck_mentee_profiles_currency", "currency IN ('VND', 'USD')");
                table.CheckConstraint("ck_mentee_profiles_current_level", "current_level IN ('Beginner', 'Intermediate', 'Advanced')");
            });

        migrationBuilder.CreateTable(
            name: "mentor_profiles",
            schema: "profiles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                bio = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                hourly_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                rating_average = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false, defaultValue: 0m),
                total_reviews = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                total_sessions = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                verification_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Unverified"),
                is_accepting_mentees = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mentor_profiles", x => x.id);
                table.CheckConstraint("ck_mentor_profiles_currency", "currency IN ('VND', 'USD')");
                table.CheckConstraint("ck_mentor_profiles_hourly_rate", "hourly_rate >= 0");
                table.CheckConstraint("ck_mentor_profiles_verification_status", "verification_status IN ('Unverified', 'Pending', 'Verified', 'Rejected')");
            });

        migrationBuilder.CreateIndex(
            name: "ux_mentee_profiles_user_id",
            schema: "profiles",
            table: "mentee_profiles",
            column: "user_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_mentor_profiles_user_id",
            schema: "profiles",
            table: "mentor_profiles",
            column: "user_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "mentee_profiles",
            schema: "profiles");

        migrationBuilder.DropTable(
            name: "mentor_profiles",
            schema: "profiles");
    }
}
