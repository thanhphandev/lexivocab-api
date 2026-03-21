using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSettingTargetLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "target_language",
                table: "user_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "English");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_language",
                table: "user_settings");
        }
    }
}
