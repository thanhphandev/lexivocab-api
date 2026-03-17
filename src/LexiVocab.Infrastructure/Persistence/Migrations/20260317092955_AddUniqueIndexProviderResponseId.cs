using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexProviderResponseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProviderResponseId",
                table: "payment_transactions",
                newName: "provider_response_id");

            migrationBuilder.AlterColumn<string>(
                name: "provider_response_id",
                table: "payment_transactions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_provider_response_id",
                table: "payment_transactions",
                column: "provider_response_id",
                unique: true,
                filter: "provider_response_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_transactions_provider_response_id",
                table: "payment_transactions");

            migrationBuilder.RenameColumn(
                name: "provider_response_id",
                table: "payment_transactions",
                newName: "ProviderResponseId");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderResponseId",
                table: "payment_transactions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
