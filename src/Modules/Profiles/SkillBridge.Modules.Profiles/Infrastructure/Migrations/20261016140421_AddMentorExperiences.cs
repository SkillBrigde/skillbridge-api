using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Profiles.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMentorExperiences : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mentor_experiences",
            schema: "profiles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                position = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                start_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                end_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                is_current = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mentor_experiences", x => x.id);
                table.CheckConstraint("ck_mentor_experiences_current_state", "(is_current AND end_date IS NULL) OR (NOT is_current AND end_date IS NOT NULL)");
                table.CheckConstraint("ck_mentor_experiences_date_range", "end_date IS NULL OR end_date >= start_date");
                table.CheckConstraint("ck_mentor_experiences_profile_id", "mentor_profile_id <> '00000000-0000-0000-0000-000000000000'");
                table.ForeignKey(
                    name: "FK_mentor_experiences_mentor_profiles_mentor_profile_id",
                    column: x => x.mentor_profile_id,
                    principalSchema: "profiles",
                    principalTable: "mentor_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_mentor_experiences_profile_start_date",
            schema: "profiles",
            table: "mentor_experiences",
            columns: new[] { "mentor_profile_id", "start_date" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "mentor_experiences",
            schema: "profiles");
    }
}
