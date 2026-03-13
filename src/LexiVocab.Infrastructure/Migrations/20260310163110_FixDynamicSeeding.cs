using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixDynamicSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create Tables First
            migrationBuilder.CreateTable(
                name: "FeatureDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    NameKey = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "VND"),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DurationDays = table.Column<int>(type: "integer", nullable: false),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlanFeatures",
                columns: table => new
                {
                    PlanDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanFeatures", x => new { x.PlanDefinitionId, x.FeatureDefinitionId });
                    table.ForeignKey(
                        name: "FK_PlanFeatures_FeatureDefinitions_FeatureDefinitionId",
                        column: x => x.FeatureDefinitionId,
                        principalTable: "FeatureDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanFeatures_PlanDefinitions_PlanDefinitionId",
                        column: x => x.PlanDefinitionId,
                        principalTable: "PlanDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 2. Seed Master Data
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            var freeId = new Guid("11111111-1111-1111-1111-111111111111");
            var premiumId = new Guid("22222222-2222-2222-2222-222222222222");
            var businessId = new Guid("33333333-3333-3333-3333-333333333333");

            migrationBuilder.InsertData(
                table: "FeatureDefinitions",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("f1111111-1111-1111-1111-111111111111"), "MAX_WORDS", seedDate, "Limit of vocabulary words saved", "Maximum Words", null },
                    { new Guid("f2222222-2222-2222-2222-222222222222"), "AI_ACCESS", seedDate, "Access to AI analysis and generation", "AI Features", null },
                    { new Guid("f3333333-3333-3333-3333-333333333333"), "SUPPORT_LEVEL", seedDate, "Customer support priority", "Support Level", null },
                    { new Guid("f4444444-4444-4444-4444-444444444444"), "EXPORT_PDF", seedDate, "Ability to export lists to PDF", "Export as PDF", null }
                });

            migrationBuilder.InsertData(
                table: "PlanDefinitions",
                columns: new[] { "Id", "CreatedAt", "Currency", "Description", "DurationDays", "IsRecommended", "Name", "NameKey", "Price", "UpdatedAt" },
                values: new object[,]
                {
                    { freeId, seedDate, "VND", "Perfect for beginners", 0, false, "Free", "free_plan", 0m, null },
                    { premiumId, seedDate, "VND", "Unlock full potential", 30, true, "Premium", "premium_plan", 199000m, null },
                    { businessId, seedDate, "VND", "For advanced learners and teams", 365, false, "Business", "business_plan", 999000m, null }
                });

            migrationBuilder.InsertData(
                table: "PlanFeatures",
                columns: new[] { "FeatureDefinitionId", "PlanDefinitionId", "Value" },
                values: new object[,]
                {
                    { new Guid("f1111111-1111-1111-1111-111111111111"), freeId, "50" },
                    { new Guid("f2222222-2222-2222-2222-222222222222"), freeId, "false" },
                    { new Guid("f3333333-3333-3333-3333-333333333333"), freeId, "Community" },
                    { new Guid("f4444444-4444-4444-4444-444444444444"), freeId, "false" },
                    { new Guid("f1111111-1111-1111-1111-111111111111"), premiumId, "1000" },
                    { new Guid("f2222222-2222-2222-2222-222222222222"), premiumId, "true" },
                    { new Guid("f3333333-3333-3333-3333-333333333333"), premiumId, "Email" },
                    { new Guid("f4444444-4444-4444-4444-444444444444"), premiumId, "true" },
                    { new Guid("f1111111-1111-1111-1111-111111111111"), businessId, "Unlimited" },
                    { new Guid("f2222222-2222-2222-2222-222222222222"), businessId, "true" },
                    { new Guid("f3333333-3333-3333-3333-333333333333"), businessId, "24/7 Priority" },
                    { new Guid("f4444444-4444-4444-4444-444444444444"), businessId, "true" }
                });

            // 3. Modify Existing Tables (Using freeId as valid default for existing rows)
            migrationBuilder.DropColumn(
                name: "plan_expiration_date",
                table: "users");

            migrationBuilder.AddColumn<Guid>(
                name: "plan_definition_id",
                table: "subscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: freeId);
                
            // Update mapping from old string column to new Guid if column exists
            migrationBuilder.Sql("UPDATE subscriptions SET plan_definition_id = '22222222-2222-2222-2222-222222222222' WHERE plan = 'Premium'");
            migrationBuilder.Sql("UPDATE subscriptions SET plan_definition_id = '33333333-3333-3333-3333-333333333333' WHERE plan = 'Business'");

            migrationBuilder.DropColumn(
                name: "plan",
                table: "subscriptions");

            migrationBuilder.AddColumn<string>(
                name: "ProviderResponseId",
                table: "payment_transactions",
                type: "text",
                nullable: true);

            // 4. Create Indexes and Constraints
            migrationBuilder.CreateIndex(
                name: "IX_subscriptions_plan_definition_id",
                table: "subscriptions",
                column: "plan_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureDefinitions_Code",
                table: "FeatureDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanFeatures_FeatureDefinitionId",
                table: "PlanFeatures",
                column: "FeatureDefinitionId");

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_PlanDefinitions_plan_definition_id",
                table: "subscriptions",
                column: "plan_definition_id",
                principalTable: "PlanDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_PlanDefinitions_plan_definition_id",
                table: "subscriptions");

            migrationBuilder.DropTable(
                name: "PlanFeatures");

            migrationBuilder.DropTable(
                name: "FeatureDefinitions");

            migrationBuilder.DropTable(
                name: "PlanDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_subscriptions_plan_definition_id",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "plan_definition_id",
                table: "subscriptions");

            migrationBuilder.DropColumn(
                name: "ProviderResponseId",
                table: "payment_transactions");

            migrationBuilder.AddColumn<DateTime>(
                name: "plan_expiration_date",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plan",
                table: "subscriptions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Free");
        }
    }
}
