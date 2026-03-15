using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncWithUI_AddValueTypeAndIntervalType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IntervalType",
                table: "PlanDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PlanDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DefaultValue",
                table: "FeatureDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ValueType",
                table: "FeatureDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f1111111-1111-1111-1111-111111111111"),
                columns: new[] { "DefaultValue", "ValueType" },
                values: new object[] { "false", "boolean" });

            migrationBuilder.UpdateData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f2222222-2222-2222-2222-222222222222"),
                columns: new[] { "DefaultValue", "ValueType" },
                values: new object[] { "false", "boolean" });

            migrationBuilder.UpdateData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f3333333-3333-3333-3333-333333333333"),
                columns: new[] { "DefaultValue", "ValueType" },
                values: new object[] { "false", "boolean" });

            migrationBuilder.UpdateData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f4444444-4444-4444-4444-444444444444"),
                columns: new[] { "DefaultValue", "ValueType" },
                values: new object[] { "false", "boolean" });

            migrationBuilder.UpdateData(
                table: "PlanDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "IntervalType", "IsActive" },
                values: new object[] { "Month", true });

            migrationBuilder.UpdateData(
                table: "PlanDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "IntervalType", "IsActive" },
                values: new object[] { "Month", true });

            migrationBuilder.UpdateData(
                table: "PlanDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "IntervalType", "IsActive" },
                values: new object[] { "Month", true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IntervalType",
                table: "PlanDefinitions");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PlanDefinitions");

            migrationBuilder.DropColumn(
                name: "DefaultValue",
                table: "FeatureDefinitions");

            migrationBuilder.DropColumn(
                name: "ValueType",
                table: "FeatureDefinitions");
        }
    }
}
