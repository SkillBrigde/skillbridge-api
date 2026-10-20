using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Recommendations.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddRecommendationReadModels : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "recommendations");

        migrationBuilder.CreateTable(
            name: "interactions",
            schema: "recommendations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_id = table.Column<Guid>(type: "uuid", nullable: false),
                skill_id = table.Column<Guid>(type: "uuid", nullable: true),
                interaction_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                deduplication_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_interactions", x => x.id);
                table.CheckConstraint("ck_interaction_type", "interaction_type IN ('Click','ViewProfile')");
            });

        migrationBuilder.CreateTable(
            name: "mentor_metrics",
            schema: "recommendations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                skill_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                prices = table.Column<string>(type: "jsonb", nullable: false),
                acceptance_rate = table.Column<double>(type: "double precision", nullable: true),
                completion_rate = table.Column<double>(type: "double precision", nullable: true),
                average_response_time_minutes = table.Column<double>(type: "double precision", nullable: true),
                repeat_mentee_count = table.Column<int>(type: "integer", nullable: false),
                total_sessions = table.Column<int>(type: "integer", nullable: false),
                average_rating = table.Column<double>(type: "double precision", nullable: true),
                total_reviews = table.Column<int>(type: "integer", nullable: false),
                last_calculated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mentor_metrics", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "recommendation_logs",
            schema: "recommendations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                algorithm_version = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                mentor_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                scores = table.Column<double[]>(type: "double precision[]", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_recommendation_logs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_interactions_deduplication_key",
            schema: "recommendations",
            table: "interactions",
            column: "deduplication_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_interactions_skill_id_created_at_utc",
            schema: "recommendations",
            table: "interactions",
            columns: new[] { "skill_id", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_interactions_user_id_created_at_utc",
            schema: "recommendations",
            table: "interactions",
            columns: new[] { "user_id", "created_at_utc" });

        migrationBuilder.CreateIndex(
            name: "IX_mentor_metrics_mentor_user_id",
            schema: "recommendations",
            table: "mentor_metrics",
            column: "mentor_user_id");

        migrationBuilder.CreateIndex(
            name: "IX_recommendation_logs_created_at_utc",
            schema: "recommendations",
            table: "recommendation_logs",
            column: "created_at_utc");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "interactions",
            schema: "recommendations");

        migrationBuilder.DropTable(
            name: "mentor_metrics",
            schema: "recommendations");

        migrationBuilder.DropTable(
            name: "recommendation_logs",
            schema: "recommendations");
    }
}
