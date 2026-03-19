using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProductionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "feature_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ValueType = table.Column<string>(type: "text", nullable: false),
                    DefaultValue = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feature_definitions", x => x.Id);
                });

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
                    meaning = table.Column<string>(type: "text", nullable: true),
                    cefr_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IsFetchFailed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_vocabularies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameKey = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_definitions", x => x.Id);
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
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "User"),
                    auth_provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    auth_provider_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    refresh_token_expiry_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false),
                    LockoutEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "plan_features",
                columns: table => new
                {
                    PlanDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_features", x => new { x.PlanDefinitionId, x.FeatureDefinitionId });
                    table.ForeignKey(
                        name: "FK_plan_features_feature_definitions_FeatureDefinitionId",
                        column: x => x.FeatureDefinitionId,
                        principalTable: "feature_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_plan_features_plan_definitions_PlanDefinitionId",
                        column: x => x.PlanDefinitionId,
                        principalTable: "plan_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plan_pricings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingCycle = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "VND"),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    LabelKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_pricings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_plan_pricings_plan_definitions_PlanDefinitionId",
                        column: x => x.PlanDefinitionId,
                        principalTable: "plan_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AdditionalInfo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
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
                    DailyNewCardLimit = table.Column<int>(type: "integer", nullable: false),
                    DailyReviewLimit = table.Column<int>(type: "integer", nullable: false),
                    preferences_json = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
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

            migrationBuilder.CreateTable(
                name: "subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Active"),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    plan_pricing_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Mock"),
                    external_subscription_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_subscriptions_plan_definitions_plan_definition_id",
                        column: x => x.plan_definition_id,
                        principalTable: "plan_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_subscriptions_plan_pricings_plan_pricing_id",
                        column: x => x.plan_pricing_id,
                        principalTable: "plan_pricings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_user_id",
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
                    tag_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.ForeignKey(
                        name: "FK_user_vocabularies_vocab_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "vocab_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    external_order_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "USD"),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancel_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    raw_payload = table.Column<string>(type: "text", nullable: true),
                    provider_response_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_transactions_users_user_id",
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
                name: "ix_audit_logs_action_timestamp",
                table: "audit_logs",
                columns: new[] { "Action", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_ip_address_timestamp",
                table: "audit_logs",
                columns: new[] { "IpAddress", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_timestamp",
                table: "audit_logs",
                column: "Timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_user_id_timestamp",
                table: "audit_logs",
                columns: new[] { "UserId", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_feature_definitions_code",
                table: "feature_definitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_vocabularies_word",
                table: "master_vocabularies",
                column: "word",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_external_order_id",
                table: "payment_transactions",
                column: "external_order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_provider_response_id",
                table: "payment_transactions",
                column: "provider_response_id",
                unique: true,
                filter: "provider_response_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_subscription_id",
                table: "payment_transactions",
                column: "subscription_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_user_id",
                table: "payment_transactions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_plan_features_FeatureDefinitionId",
                table: "plan_features",
                column: "FeatureDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_pricings_PlanDefinitionId",
                table: "plan_pricings",
                column: "PlanDefinitionId");

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
                name: "IX_subscriptions_plan_definition_id",
                table: "subscriptions",
                column: "plan_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_plan_pricing_id",
                table: "subscriptions",
                column: "plan_pricing_id");

            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_user_id",
                table: "subscriptions",
                column: "user_id");

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
                name: "IX_user_vocabularies_tag_id",
                table: "user_vocabularies",
                column: "tag_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_vocab_tags_user_id",
                table: "vocab_tags",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_vocab_tags_user_slug",
                table: "vocab_tags",
                columns: new[] { "user_id", "slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "payment_transactions");

            migrationBuilder.DropTable(
                name: "plan_features");

            migrationBuilder.DropTable(
                name: "review_logs");

            migrationBuilder.DropTable(
                name: "user_settings");

            migrationBuilder.DropTable(
                name: "subscriptions");

            migrationBuilder.DropTable(
                name: "feature_definitions");

            migrationBuilder.DropTable(
                name: "user_vocabularies");

            migrationBuilder.DropTable(
                name: "plan_pricings");

            migrationBuilder.DropTable(
                name: "master_vocabularies");

            migrationBuilder.DropTable(
                name: "vocab_tags");

            migrationBuilder.DropTable(
                name: "plan_definitions");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
