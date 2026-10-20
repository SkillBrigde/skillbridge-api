using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Booking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddOutboxRetryAfter : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "retry_after_utc",
            schema: "booking",
            table: "outbox_messages",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "retry_after_utc",
            schema: "booking",
            table: "outbox_messages");
    }
}
