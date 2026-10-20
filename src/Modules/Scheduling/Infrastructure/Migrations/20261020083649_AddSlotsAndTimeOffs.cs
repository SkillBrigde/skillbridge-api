using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Scheduling.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddSlotsAndTimeOffs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "availability_slots",
            schema: "scheduling",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                start_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                end_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                held_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_availability_slots", x => x.id);
                table.CheckConstraint("ck_slots_booking", "(status = 'Held' AND booking_id IS NOT NULL AND held_until_utc IS NOT NULL) OR (status = 'Booked' AND booking_id IS NOT NULL AND held_until_utc IS NULL) OR (status IN ('Available','Locked','Unavailable') AND booking_id IS NULL AND held_until_utc IS NULL)");
                table.CheckConstraint("ck_slots_status", "status IN ('Available','Locked','Unavailable','Held','Booked')");
                table.CheckConstraint("ck_slots_window", "end_at_utc > start_at_utc");
            });

        migrationBuilder.CreateTable(
            name: "time_offs",
            schema: "scheduling",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                start_date = table.Column<DateOnly>(type: "date", nullable: false),
                end_date = table.Column<DateOnly>(type: "date", nullable: false),
                reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_time_offs", x => x.id);
                table.CheckConstraint("ck_time_off_dates", "end_date >= start_date");
            });

        migrationBuilder.CreateIndex(
            name: "IX_availability_slots_booking_id",
            schema: "scheduling",
            table: "availability_slots",
            column: "booking_id");

        migrationBuilder.CreateIndex(
            name: "IX_availability_slots_mentor_id_start_at_utc_end_at_utc",
            schema: "scheduling",
            table: "availability_slots",
            columns: new[] { "mentor_id", "start_at_utc", "end_at_utc" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_time_offs_mentor_id_start_date_end_date",
            schema: "scheduling",
            table: "time_offs",
            columns: new[] { "mentor_id", "start_date", "end_date" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "availability_slots",
            schema: "scheduling");

        migrationBuilder.DropTable(
            name: "time_offs",
            schema: "scheduling");
    }
}
