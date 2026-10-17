using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Messaging.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Initial_Messaging : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "messaging");

        migrationBuilder.CreateTable(
            name: "conversations",
            schema: "messaging",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                participant1_id = table.Column<Guid>(type: "uuid", nullable: false),
                participant2_id = table.Column<Guid>(type: "uuid", nullable: false),
                last_sequence = table.Column<long>(type: "bigint", nullable: false),
                last_message_preview = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                last_message_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_conversations", x => x.id);
                table.CheckConstraint("CK_conversations_participants", "participant1_id < participant2_id");
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "messaging",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
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
            name: "message_reads",
            schema: "messaging",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                last_read_sequence = table.Column<long>(type: "bigint", nullable: false),
                last_read_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                last_read_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_message_reads", x => x.id);
                table.ForeignKey(
                    name: "FK_message_reads_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalSchema: "messaging",
                    principalTable: "conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "messages",
            schema: "messaging",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                sequence = table.Column<long>(type: "bigint", nullable: false),
                content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                is_edited = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                edited_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_messages", x => x.id);
                table.CheckConstraint("CK_messages_sequence", "sequence > 0");
                table.ForeignKey(
                    name: "FK_messages_conversations_conversation_id",
                    column: x => x.conversation_id,
                    principalSchema: "messaging",
                    principalTable: "conversations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_conversations_participant1_id_participant2_id",
            schema: "messaging",
            table: "conversations",
            columns: new[] { "participant1_id", "participant2_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_conversations_participant2_id",
            schema: "messaging",
            table: "conversations",
            column: "participant2_id");

        migrationBuilder.CreateIndex(
            name: "IX_message_reads_conversation_id_user_id",
            schema: "messaging",
            table: "message_reads",
            columns: new[] { "conversation_id", "user_id" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_messages_conversation_id_sequence",
            schema: "messaging",
            table: "messages",
            columns: new[] { "conversation_id", "sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_occurred_on_utc",
            schema: "messaging",
            table: "outbox_messages",
            column: "occurred_on_utc",
            filter: "processed_on_utc IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "message_reads",
            schema: "messaging");

        migrationBuilder.DropTable(
            name: "messages",
            schema: "messaging");

        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "messaging");

        migrationBuilder.DropTable(
            name: "conversations",
            schema: "messaging");
    }
}
