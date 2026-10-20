using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Learning.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddLearningSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "learning");

        migrationBuilder.CreateTable(
            name: "inbox_messages",
            schema: "learning",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                processed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_inbox_messages", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "learning_sessions",
            schema: "learning",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_number = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                room_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                scheduled_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                scheduled_end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                actual_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                actual_end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_learning_sessions", x => x.id);
                table.CheckConstraint("ck_learning_completion", "status <> 'Completed' OR (actual_start_utc IS NOT NULL AND actual_end_utc IS NOT NULL AND actual_end_utc >= scheduled_end_utc AND actual_end_utc > actual_start_utc)");
                table.CheckConstraint("ck_learning_status", "status IN ('Scheduled','InProgress','Completed','Cancelled')");
                table.CheckConstraint("ck_learning_window", "scheduled_end_utc > scheduled_start_utc");
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "learning",
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
            name: "session_action_items",
            schema: "learning",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                assigned_to = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                is_completed = table.Column<bool>(type: "boolean", nullable: false),
                completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_session_action_items", x => x.id);
                table.ForeignKey(
                    name: "FK_session_action_items_learning_sessions_session_id",
                    column: x => x.session_id,
                    principalSchema: "learning",
                    principalTable: "learning_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "session_attendances",
            schema: "learning",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                joined_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                left_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_session_attendances", x => x.id);
                table.CheckConstraint("ck_attendance_window", "left_at_utc IS NULL OR left_at_utc >= joined_at_utc");
                table.ForeignKey(
                    name: "FK_session_attendances_learning_sessions_session_id",
                    column: x => x.session_id,
                    principalSchema: "learning",
                    principalTable: "learning_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "session_materials",
            schema: "learning",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                file_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                data = table.Column<byte[]>(type: "bytea", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_session_materials", x => x.id);
                table.CheckConstraint("ck_material_size", "file_size_bytes BETWEEN 1 AND 10485760 AND octet_length(data) = file_size_bytes");
                table.ForeignKey(
                    name: "FK_session_materials_learning_sessions_session_id",
                    column: x => x.session_id,
                    principalSchema: "learning",
                    principalTable: "learning_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "session_notes",
            schema: "learning",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                author_id = table.Column<Guid>(type: "uuid", nullable: false),
                content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                is_private = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_session_notes", x => x.id);
                table.ForeignKey(
                    name: "FK_session_notes_learning_sessions_session_id",
                    column: x => x.session_id,
                    principalSchema: "learning",
                    principalTable: "learning_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_learning_sessions_booking_id",
            schema: "learning",
            table: "learning_sessions",
            column: "booking_id");

        migrationBuilder.CreateIndex(
            name: "IX_learning_sessions_booking_session_id",
            schema: "learning",
            table: "learning_sessions",
            column: "booking_session_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_learning_sessions_mentee_user_id_scheduled_start_utc",
            schema: "learning",
            table: "learning_sessions",
            columns: new[] { "mentee_user_id", "scheduled_start_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_learning_sessions_mentor_user_id_scheduled_start_utc",
            schema: "learning",
            table: "learning_sessions",
            columns: new[] { "mentor_user_id", "scheduled_start_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_occurred_on_utc",
            schema: "learning",
            table: "outbox_messages",
            column: "occurred_on_utc",
            filter: "processed_on_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_session_action_items_session_id_created_at_utc",
            schema: "learning",
            table: "session_action_items",
            columns: new[] { "session_id", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_session_attendances_session_id_user_id",
            schema: "learning",
            table: "session_attendances",
            columns: new[] { "session_id", "user_id" },
            unique: true,
            filter: "left_at_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_session_materials_session_id_created_at_utc",
            schema: "learning",
            table: "session_materials",
            columns: new[] { "session_id", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_session_notes_session_id_created_at_utc",
            schema: "learning",
            table: "session_notes",
            columns: new[] { "session_id", "created_at_utc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "inbox_messages",
            schema: "learning");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "learning");

        migrationBuilder.DropTable(
            name: "session_action_items",
            schema: "learning");

        migrationBuilder.DropTable(
            name: "session_attendances",
            schema: "learning");

        migrationBuilder.DropTable(
            name: "session_materials",
            schema: "learning");

        migrationBuilder.DropTable(
            name: "session_notes",
            schema: "learning");

        migrationBuilder.DropTable(
            name: "learning_sessions",
            schema: "learning");
    }
}
