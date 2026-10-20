using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Reviews.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddVerifiedReviews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "reviews");

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "reviews",
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
            name: "reviews",
            schema: "reviews",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                session_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentee_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                overall_rating = table.Column<int>(type: "integer", nullable: false),
                communication_rating = table.Column<int>(type: "integer", nullable: false),
                expertise_rating = table.Column<int>(type: "integer", nullable: false),
                helpfulness_rating = table.Column<int>(type: "integer", nullable: false),
                comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                mentor_reply = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                mentor_replied_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                is_visible = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reviews", x => x.id);
                table.CheckConstraint("ck_review_ratings", "overall_rating BETWEEN 1 AND 5 AND communication_rating BETWEEN 1 AND 5 AND expertise_rating BETWEEN 1 AND 5 AND helpfulness_rating BETWEEN 1 AND 5");
            });

        migrationBuilder.CreateIndex(
            name: "IX_reviews_booking_id",
            schema: "reviews",
            table: "reviews",
            column: "booking_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_reviews_mentee_user_id",
            schema: "reviews",
            table: "reviews",
            column: "mentee_user_id");

        migrationBuilder.CreateIndex(
            name: "IX_reviews_mentor_id_is_visible_created_at_utc",
            schema: "reviews",
            table: "reviews",
            columns: new[] { "mentor_id", "is_visible", "created_at_utc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "reviews");

        migrationBuilder.DropTable(
            name: "reviews",
            schema: "reviews");
    }
}
