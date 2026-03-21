using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNativeLanguageToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NativeLanguage",
                table: "user_settings",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NativeLanguage",
                table: "user_settings");
        }
    }
}
