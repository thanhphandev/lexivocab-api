using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "NativeLanguage",
                table: "user_settings",
                newName: "native_language");

            migrationBuilder.AlterColumn<string>(
                name: "target_language",
                table: "user_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "en",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "English");

            migrationBuilder.AlterColumn<string>(
                name: "native_language",
                table: "user_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "vi",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "IsEmailReminderEnabled",
                table: "user_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTelegramReminderEnabled",
                table: "user_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsZaloReminderEnabled",
                table: "user_settings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TelegramBotToken",
                table: "user_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramChatId",
                table: "user_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZaloBotToken",
                table: "user_settings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZaloUserId",
                table: "user_settings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsEmailReminderEnabled",
                table: "user_settings");

            migrationBuilder.DropColumn(
                name: "IsTelegramReminderEnabled",
                table: "user_settings");

            migrationBuilder.DropColumn(
                name: "IsZaloReminderEnabled",
                table: "user_settings");

            migrationBuilder.DropColumn(
                name: "TelegramBotToken",
                table: "user_settings");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                table: "user_settings");

            migrationBuilder.DropColumn(
                name: "ZaloBotToken",
                table: "user_settings");

            migrationBuilder.DropColumn(
                name: "ZaloUserId",
                table: "user_settings");

            migrationBuilder.RenameColumn(
                name: "native_language",
                table: "user_settings",
                newName: "NativeLanguage");

            migrationBuilder.AlterColumn<string>(
                name: "target_language",
                table: "user_settings",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "English",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "en");

            migrationBuilder.AlterColumn<string>(
                name: "NativeLanguage",
                table: "user_settings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "vi");
        }
    }
}
