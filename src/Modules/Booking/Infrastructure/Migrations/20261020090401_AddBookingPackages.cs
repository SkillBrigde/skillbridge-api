using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Booking.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddBookingPackages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "booking");

        migrationBuilder.CreateTable(
            name: "bookings",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                terms = table.Column<string>(type: "jsonb", nullable: false),
                intake = table.Column<string>(type: "jsonb", nullable: false),
                idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                booking_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                held_until_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                mentor_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                cancellation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                needs_slot_release = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_bookings", x => x.id);
                table.CheckConstraint("ck_booking_status", "status IN ('Holding','PendingPayment','AwaitingMentor','Confirmed','Completed','Cancelled','RefundReview')");
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                content = table.Column<string>(type: "jsonb", nullable: false),
                occurred_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                processed_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "booking_audit_logs",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                old_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                new_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_booking_audit_logs", x => x.id);
                table.ForeignKey(
                    name: "FK_booking_audit_logs_bookings_booking_id",
                    column: x => x.booking_id,
                    principalSchema: "booking",
                    principalTable: "bookings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "booking_sessions",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_number = table.Column<int>(type: "integer", nullable: false),
                start_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                end_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_booking_sessions", x => x.id);
                table.CheckConstraint("ck_booking_session_window", "(start_at_utc IS NULL AND end_at_utc IS NULL) OR (start_at_utc IS NOT NULL AND end_at_utc > start_at_utc)");
                table.ForeignKey(
                    name: "FK_booking_sessions_bookings_booking_id",
                    column: x => x.booking_id,
                    principalSchema: "booking",
                    principalTable: "bookings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "reschedule_requests",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                old_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                new_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                response_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reschedule_requests", x => x.id);
                table.CheckConstraint("ck_reschedule_status", "status IN ('Pending','Applying','Approved','Rejected')");
                table.ForeignKey(
                    name: "FK_reschedule_requests_booking_sessions_booking_session_id",
                    column: x => x.booking_session_id,
                    principalSchema: "booking",
                    principalTable: "booking_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_reschedule_requests_bookings_booking_id",
                    column: x => x.booking_id,
                    principalSchema: "booking",
                    principalTable: "bookings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_booking_audit_logs_booking_id_created_at_utc",
            schema: "booking",
            table: "booking_audit_logs",
            columns: new[] { "booking_id", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_booking_sessions_booking_id_slot_id",
            schema: "booking",
            table: "booking_sessions",
            columns: new[] { "booking_id", "slot_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_bookings_booking_code",
            schema: "booking",
            table: "bookings",
            column: "booking_code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_bookings_mentee_user_id_idempotency_key",
            schema: "booking",
            table: "bookings",
            columns: new[] { "mentee_user_id", "idempotency_key" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_bookings_mentor_user_id",
            schema: "booking",
            table: "bookings",
            column: "mentor_user_id");

        migrationBuilder.CreateIndex(
            name: "IX_bookings_payment_id",
            schema: "booking",
            table: "bookings",
            column: "payment_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_bookings_status_held_until_utc",
            schema: "booking",
            table: "bookings",
            columns: new[] { "status", "held_until_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_occurred_on_utc",
            schema: "booking",
            table: "outbox_messages",
            column: "occurred_on_utc",
            filter: "processed_on_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_reschedule_requests_booking_id",
            schema: "booking",
            table: "reschedule_requests",
            column: "booking_id");

        migrationBuilder.CreateIndex(
            name: "IX_reschedule_requests_booking_session_id",
            schema: "booking",
            table: "reschedule_requests",
            column: "booking_session_id",
            unique: true,
            filter: "status IN ('Pending','Applying')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "booking_audit_logs",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "reschedule_requests",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "booking_sessions",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "bookings",
            schema: "booking");
    }
}
