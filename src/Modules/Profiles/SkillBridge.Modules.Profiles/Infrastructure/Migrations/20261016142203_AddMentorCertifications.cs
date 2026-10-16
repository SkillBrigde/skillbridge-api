using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Profiles.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorCertifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mentor_certifications",
                schema: "profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentor_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issuing_organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    issue_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expiry_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    credential_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    credential_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentor_certifications", x => x.id);
                    table.CheckConstraint("ck_mentor_certifications_date_range", "expiry_date IS NULL OR expiry_date >= issue_date");
                    table.CheckConstraint("ck_mentor_certifications_profile_id", "mentor_profile_id <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_mentor_certifications_mentor_profiles_mentor_profile_id",
                        column: x => x.mentor_profile_id,
                        principalSchema: "profiles",
                        principalTable: "mentor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mentor_certifications_profile_issue_date",
                schema: "profiles",
                table: "mentor_certifications",
                columns: new[] { "mentor_profile_id", "issue_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mentor_certifications",
                schema: "profiles");
        }
    }
}
