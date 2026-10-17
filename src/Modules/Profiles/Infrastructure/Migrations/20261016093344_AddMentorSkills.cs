using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Profiles.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMentorSkills : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "mentor_skills",
            schema: "profiles",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                mentor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                skill_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                proficiency_level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                years_of_experience = table.Column<int>(type: "integer", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_mentor_skills", x => x.id);
                table.CheckConstraint("ck_mentor_skills_ids", "mentor_profile_id <> '00000000-0000-0000-0000-000000000000' AND skill_id <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("ck_mentor_skills_proficiency_level", "proficiency_level IN ('Beginner', 'Intermediate', 'Advanced', 'Expert')");
                table.CheckConstraint("ck_mentor_skills_years_of_experience", "years_of_experience >= 0");
                table.ForeignKey(
                    name: "FK_mentor_skills_mentor_profiles_mentor_profile_id",
                    column: x => x.mentor_profile_id,
                    principalSchema: "profiles",
                    principalTable: "mentor_profiles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_mentor_skills_skill_id",
            schema: "profiles",
            table: "mentor_skills",
            column: "skill_id");

        migrationBuilder.CreateIndex(
            name: "ux_mentor_skills_profile_skill",
            schema: "profiles",
            table: "mentor_skills",
            columns: new[] { "mentor_profile_id", "skill_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "mentor_skills",
            schema: "profiles");
    }
}
