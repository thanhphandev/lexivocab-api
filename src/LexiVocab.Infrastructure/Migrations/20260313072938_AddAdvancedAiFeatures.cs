using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedAiFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailConfirmed",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            var advancedAiId = new Guid("f5555555-5555-5555-5555-555555555555");
            var quizId = new Guid("f6666666-6666-6666-6666-666666666666");
            var dailyLimitId = new Guid("f7777777-7777-7777-7777-777777777777");
            var freeId = new Guid("11111111-1111-1111-1111-111111111111");
            var premiumId = new Guid("22222222-2222-2222-2222-222222222222");
            var businessId = new Guid("33333333-3333-3333-3333-333333333333");
            var seedDate = DateTime.UtcNow;

            migrationBuilder.InsertData(
                table: "FeatureDefinitions",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "Name" },
                values: new object[,]
                {
                    { advancedAiId, "ADVANCED_AI", seedDate, "Detailed usage explanation and nuances", "Advanced AI Analysis" },
                    { quizId, "QUIZ_GENERATION", seedDate, "Generate custom quizzes for vocabulary", "Quiz Generation" },
                    { dailyLimitId, "AI_DAILY_LIMIT", seedDate, "Daily quota for AI requests", "AI Daily Quota" }
                });

            migrationBuilder.InsertData(
                table: "PlanFeatures",
                columns: new[] { "FeatureDefinitionId", "PlanDefinitionId", "Value" },
                values: new object[,]
                {
                    { advancedAiId, premiumId, "true" },
                    { quizId, premiumId, "true" },
                    { dailyLimitId, premiumId, "100" },
                    { advancedAiId, businessId, "true" },
                    { quizId, businessId, "true" },
                    { dailyLimitId, businessId, "500" },
                    { dailyLimitId, freeId, "0" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailConfirmed",
                table: "users");

            migrationBuilder.DeleteData(
                table: "PlanFeatures",
                keyColumns: new[] { "FeatureDefinitionId", "PlanDefinitionId" },
                keyValues: new object[,]
                {
                    { new Guid("f5555555-5555-5555-5555-555555555555"), new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("f6666666-6666-6666-6666-666666666666"), new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("f5555555-5555-5555-5555-555555555555"), new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("f6666666-6666-6666-6666-666666666666"), new Guid("33333333-3333-3333-3333-333333333333") },
                    { new Guid("f7777777-7777-7777-7777-777777777777"), new Guid("11111111-1111-1111-1111-111111111111") },
                    { new Guid("f7777777-7777-7777-7777-777777777777"), new Guid("22222222-2222-2222-2222-222222222222") },
                    { new Guid("f7777777-7777-7777-7777-777777777777"), new Guid("33333333-3333-3333-3333-333333333333") }
                });

            migrationBuilder.DeleteData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f5555555-5555-5555-5555-555555555555"));

            migrationBuilder.DeleteData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f6666666-6666-6666-6666-666666666666"));

            migrationBuilder.DeleteData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f7777777-7777-7777-7777-777777777777"));
        }
    }
}
