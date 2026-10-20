using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Profiles.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddMentorSkillPrimary : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "is_primary",
            schema: "profiles",
            table: "mentor_skills",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "is_primary",
            schema: "profiles",
            table: "mentor_skills");
    }
}
