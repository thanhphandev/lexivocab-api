using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tag_id",
                table: "user_vocabularies",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vocab_tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, defaultValue: "#6366F1"),
                    icon = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, defaultValue: "📁"),
                    is_auto_generated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    source_domain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    word_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vocab_tags", x => x.id);
                    table.ForeignKey(
                        name: "FK_vocab_tags_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_vocabularies_tag_id",
                table: "user_vocabularies",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_vocab_tags_user_id",
                table: "vocab_tags",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vocab_tags_user_slug",
                table: "vocab_tags",
                columns: new[] { "user_id", "slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_user_vocabularies_vocab_tags_tag_id",
                table: "user_vocabularies",
                column: "tag_id",
                principalTable: "vocab_tags",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_vocabularies_vocab_tags_tag_id",
                table: "user_vocabularies");

            migrationBuilder.DropTable(
                name: "vocab_tags");

            migrationBuilder.DropIndex(
                name: "IX_user_vocabularies_tag_id",
                table: "user_vocabularies");

            migrationBuilder.DropColumn(
                name: "tag_id",
                table: "user_vocabularies");
        }
    }
}
