using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Scheduling.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Initial_Scheduling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "scheduling");

        migrationBuilder.CreateTable(
            name: "scheduling_settings",
            schema: "scheduling",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                buffer_time_minutes = table.Column<int>(type: "integer", nullable: false),
                lead_time_hours = table.Column<int>(type: "integer", nullable: false),
                max_days_in_advance = table.Column<int>(type: "integer", nullable: false),
                timezone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_scheduling_settings", x => x.id);
                table.CheckConstraint("ck_scheduling_settings_advance_window", "max_days_in_advance BETWEEN 1 AND 365 AND lead_time_hours <= max_days_in_advance * 24");
                table.CheckConstraint("ck_scheduling_settings_buffer_time", "buffer_time_minutes BETWEEN 0 AND 120 AND buffer_time_minutes % 5 = 0");
                table.CheckConstraint("ck_scheduling_settings_lead_time", "lead_time_hours BETWEEN 0 AND 720");
                table.CheckConstraint("ck_scheduling_settings_mentor_id", "mentor_id <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("ck_scheduling_settings_session_duration", "session_duration_minutes BETWEEN 15 AND 240 AND session_duration_minutes % 15 = 0");
            });

        migrationBuilder.CreateIndex(
            name: "ux_scheduling_settings_mentor_id",
            schema: "scheduling",
            table: "scheduling_settings",
            column: "mentor_id",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "scheduling_settings",
            schema: "scheduling");
    }
}
