using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase5CommunityModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_transactions_Coupons_CouponId",
                table: "payment_transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Coupons",
                table: "Coupons");

            migrationBuilder.RenameTable(
                name: "Coupons",
                newName: "coupons");

            migrationBuilder.RenameIndex(
                name: "IX_Coupons_Code",
                table: "coupons",
                newName: "IX_coupons_Code");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "master_vocabularies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "master_vocabularies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddPrimaryKey(
                name: "PK_coupons",
                table: "coupons",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_transactions_coupons_CouponId",
                table: "payment_transactions",
                column: "CouponId",
                principalTable: "coupons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_transactions_coupons_CouponId",
                table: "payment_transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_coupons",
                table: "coupons");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "master_vocabularies");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "master_vocabularies");

            migrationBuilder.RenameTable(
                name: "coupons",
                newName: "Coupons");

            migrationBuilder.RenameIndex(
                name: "IX_coupons_Code",
                table: "Coupons",
                newName: "IX_Coupons_Code");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Coupons",
                table: "Coupons",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_transactions_Coupons_CouponId",
                table: "payment_transactions",
                column: "CouponId",
                principalTable: "Coupons",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
