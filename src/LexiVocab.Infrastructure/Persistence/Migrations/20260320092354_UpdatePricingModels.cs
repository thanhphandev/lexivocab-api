using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePricingModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "coupons",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Currency",
                table: "coupons");
        }
    }
}
