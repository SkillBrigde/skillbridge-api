using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Scheduling.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAvailabilityRules : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "availability_rules",
            schema: "scheduling",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                day_of_week = table.Column<int>(type: "integer", nullable: false),
                start_time = table.Column<TimeOnly>(type: "time(0) without time zone", precision: 0, nullable: false),
                end_time = table.Column<TimeOnly>(type: "time(0) without time zone", precision: 0, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_availability_rules", x => x.id);
                table.CheckConstraint("ck_availability_rules_day_of_week", "day_of_week BETWEEN 0 AND 6");
                table.CheckConstraint("ck_availability_rules_mentor_id", "mentor_id <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("ck_availability_rules_time_range", "start_time < end_time");
            });

        migrationBuilder.CreateIndex(
            name: "ix_availability_rules_mentor_day_start",
            schema: "scheduling",
            table: "availability_rules",
            columns: new[] { "mentor_id", "day_of_week", "start_time", "id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "availability_rules",
            schema: "scheduling");
    }
}
