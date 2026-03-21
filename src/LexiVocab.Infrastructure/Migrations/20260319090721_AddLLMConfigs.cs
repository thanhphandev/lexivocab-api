using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLLMConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultTranslator",
                table: "user_settings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultTranslator",
                table: "user_settings");
        }
    }
}
