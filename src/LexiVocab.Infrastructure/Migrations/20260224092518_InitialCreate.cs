using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_vocabularies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    word = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    part_of_speech = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    phonetic_uk = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phonetic_us = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    audio_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    popularity_rank = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_vocabularies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_login = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "User"),
                    auth_provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    auth_provider_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    refresh_token_expiry_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_highlight_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    highlight_color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "#FFD700"),
                    excluded_domains = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'::jsonb"),
                    daily_goal = table.Column<int>(type: "integer", nullable: false, defaultValue: 20),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_vocabularies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    master_vocabulary_id = table.Column<Guid>(type: "uuid", nullable: true),
                    word_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    custom_meaning = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    context_sentence = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    repetition_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    easiness_factor = table.Column<double>(type: "double precision", nullable: false, defaultValue: 2.5),
                    interval_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_review_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_vocabularies", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_vocabularies_master_vocabularies_master_vocabulary_id",
                        column: x => x.master_vocabulary_id,
                        principalTable: "master_vocabularies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_user_vocabularies_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "review_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_vocabulary_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quality_score = table.Column<short>(type: "smallint", nullable: false),
                    time_spent_ms = table.Column<int>(type: "integer", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_review_logs_user_vocabularies_user_vocabulary_id",
                        column: x => x.user_vocabulary_id,
                        principalTable: "user_vocabularies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_review_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_master_vocabularies_word",
                table: "master_vocabularies",
                column: "word",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_logs_reviewed_at",
                table: "review_logs",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ix_review_logs_user_id",
                table: "review_logs",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_logs_user_reviewed_at",
                table: "review_logs",
                columns: new[] { "user_id", "reviewed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_review_logs_user_vocabulary_id",
                table: "review_logs",
                column: "user_vocabulary_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_settings_user_id",
                table: "user_settings",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_vocabularies_master_vocabulary_id",
                table: "user_vocabularies",
                column: "master_vocabulary_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_vocabularies_next_review_date",
                table: "user_vocabularies",
                column: "next_review_date");

            migrationBuilder.CreateIndex(
                name: "ix_user_vocabularies_user_id",
                table: "user_vocabularies",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_vocabularies_user_review_archive",
                table: "user_vocabularies",
                columns: new[] { "user_id", "next_review_date", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "review_logs");

            migrationBuilder.DropTable(
                name: "user_settings");

            migrationBuilder.DropTable(
                name: "user_vocabularies");

            migrationBuilder.DropTable(
                name: "master_vocabularies");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
