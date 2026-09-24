using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SkillBridge.Modules.Catalog.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Initial_Catalog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "catalog");

        migrationBuilder.CreateTable(
            name: "categories",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                slug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                icon_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                display_order = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                deleted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_categories", x => x.id);
                table.CheckConstraint("CK_categories_display_order", "display_order >= 0");
                table.CheckConstraint("CK_categories_parent", "parent_id IS NULL OR parent_id <> id");
                table.ForeignKey(
                    name: "FK_categories_categories_parent_id",
                    column: x => x.parent_id,
                    principalSchema: "catalog",
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "catalog",
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
            name: "tags",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                slug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tags", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "skills",
            schema: "catalog",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                category_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                slug = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_skills", x => x.id);
                table.ForeignKey(
                    name: "FK_skills_categories_category_id",
                    column: x => x.category_id,
                    principalSchema: "catalog",
                    principalTable: "categories",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "skill_tags",
            schema: "catalog",
            columns: table => new
            {
                skill_id = table.Column<Guid>(type: "uuid", nullable: false),
                tag_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_skill_tags", x => new { x.skill_id, x.tag_id });
                table.ForeignKey(
                    name: "FK_skill_tags_skills_skill_id",
                    column: x => x.skill_id,
                    principalSchema: "catalog",
                    principalTable: "skills",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_skill_tags_tags_tag_id",
                    column: x => x.tag_id,
                    principalSchema: "catalog",
                    principalTable: "tags",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_categories_parent_id",
            schema: "catalog",
            table: "categories",
            column: "parent_id");

        migrationBuilder.CreateIndex(
            name: "IX_categories_slug",
            schema: "catalog",
            table: "categories",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_occurred_on_utc",
            schema: "catalog",
            table: "outbox_messages",
            column: "occurred_on_utc",
            filter: "processed_on_utc IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_skill_tags_tag_id",
            schema: "catalog",
            table: "skill_tags",
            column: "tag_id");

        migrationBuilder.CreateIndex(
            name: "IX_skills_category_id",
            schema: "catalog",
            table: "skills",
            column: "category_id");

        migrationBuilder.CreateIndex(
            name: "IX_skills_slug",
            schema: "catalog",
            table: "skills",
            column: "slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_tags_slug",
            schema: "catalog",
            table: "tags",
            column: "slug",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "skill_tags",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "skills",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "tags",
            schema: "catalog");

        migrationBuilder.DropTable(
            name: "categories",
            schema: "catalog");
    }
}
