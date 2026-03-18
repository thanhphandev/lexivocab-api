using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialProductionSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_users_UserId",
                table: "AuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanFeatures_FeatureDefinitions_FeatureDefinitionId",
                table: "PlanFeatures");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanFeatures_PlanDefinitions_PlanDefinitionId",
                table: "PlanFeatures");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanPricings_PlanDefinitions_PlanDefinitionId",
                table: "PlanPricings");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_PlanDefinitions_plan_definition_id",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_PlanPricings_plan_pricing_id",
                table: "subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanPricings",
                table: "PlanPricings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanFeatures",
                table: "PlanFeatures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PlanDefinitions",
                table: "PlanDefinitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeatureDefinitions",
                table: "FeatureDefinitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs");

            migrationBuilder.RenameTable(
                name: "PlanPricings",
                newName: "plan_pricings");

            migrationBuilder.RenameTable(
                name: "PlanFeatures",
                newName: "plan_features");

            migrationBuilder.RenameTable(
                name: "PlanDefinitions",
                newName: "plan_definitions");

            migrationBuilder.RenameTable(
                name: "FeatureDefinitions",
                newName: "feature_definitions");

            migrationBuilder.RenameTable(
                name: "AuditLogs",
                newName: "audit_logs");

            migrationBuilder.RenameIndex(
                name: "IX_master_vocabularies_word",
                table: "master_vocabularies",
                newName: "ix_master_vocabularies_word");

            migrationBuilder.RenameIndex(
                name: "IX_PlanPricings_PlanDefinitionId",
                table: "plan_pricings",
                newName: "IX_plan_pricings_PlanDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_PlanFeatures_FeatureDefinitionId",
                table: "plan_features",
                newName: "IX_plan_features_FeatureDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_FeatureDefinitions_Code",
                table: "feature_definitions",
                newName: "ix_feature_definitions_code");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "audit_logs",
                newName: "ix_audit_logs_user_id_timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "audit_logs",
                newName: "ix_audit_logs_timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_IpAddress_Timestamp",
                table: "audit_logs",
                newName: "ix_audit_logs_ip_address_timestamp");

            migrationBuilder.RenameIndex(
                name: "IX_AuditLogs_Action_Timestamp",
                table: "audit_logs",
                newName: "ix_audit_logs_action_timestamp");

            migrationBuilder.AddPrimaryKey(
                name: "PK_plan_pricings",
                table: "plan_pricings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_plan_features",
                table: "plan_features",
                columns: new[] { "PlanDefinitionId", "FeatureDefinitionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_plan_definitions",
                table: "plan_definitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_feature_definitions",
                table: "feature_definitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_audit_logs",
                table: "audit_logs",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "feature_definitions",
                keyColumn: "Id",
                keyValue: new Guid("f1111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "feature_definitions",
                keyColumn: "Id",
                keyValue: new Guid("f2222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "feature_definitions",
                keyColumn: "Id",
                keyValue: new Guid("f3333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "feature_definitions",
                keyColumn: "Id",
                keyValue: new Guid("f4444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddForeignKey(
                name: "FK_audit_logs_users_UserId",
                table: "audit_logs",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_plan_features_feature_definitions_FeatureDefinitionId",
                table: "plan_features",
                column: "FeatureDefinitionId",
                principalTable: "feature_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_plan_features_plan_definitions_PlanDefinitionId",
                table: "plan_features",
                column: "PlanDefinitionId",
                principalTable: "plan_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_plan_pricings_plan_definitions_PlanDefinitionId",
                table: "plan_pricings",
                column: "PlanDefinitionId",
                principalTable: "plan_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_plan_definitions_plan_definition_id",
                table: "subscriptions",
                column: "plan_definition_id",
                principalTable: "plan_definitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_plan_pricings_plan_pricing_id",
                table: "subscriptions",
                column: "plan_pricing_id",
                principalTable: "plan_pricings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_audit_logs_users_UserId",
                table: "audit_logs");

            migrationBuilder.DropForeignKey(
                name: "FK_plan_features_feature_definitions_FeatureDefinitionId",
                table: "plan_features");

            migrationBuilder.DropForeignKey(
                name: "FK_plan_features_plan_definitions_PlanDefinitionId",
                table: "plan_features");

            migrationBuilder.DropForeignKey(
                name: "FK_plan_pricings_plan_definitions_PlanDefinitionId",
                table: "plan_pricings");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_plan_definitions_plan_definition_id",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_plan_pricings_plan_pricing_id",
                table: "subscriptions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_plan_pricings",
                table: "plan_pricings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_plan_features",
                table: "plan_features");

            migrationBuilder.DropPrimaryKey(
                name: "PK_plan_definitions",
                table: "plan_definitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_feature_definitions",
                table: "feature_definitions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_audit_logs",
                table: "audit_logs");

            migrationBuilder.RenameTable(
                name: "plan_pricings",
                newName: "PlanPricings");

            migrationBuilder.RenameTable(
                name: "plan_features",
                newName: "PlanFeatures");

            migrationBuilder.RenameTable(
                name: "plan_definitions",
                newName: "PlanDefinitions");

            migrationBuilder.RenameTable(
                name: "feature_definitions",
                newName: "FeatureDefinitions");

            migrationBuilder.RenameTable(
                name: "audit_logs",
                newName: "AuditLogs");

            migrationBuilder.RenameIndex(
                name: "ix_master_vocabularies_word",
                table: "master_vocabularies",
                newName: "IX_master_vocabularies_word");

            migrationBuilder.RenameIndex(
                name: "IX_plan_pricings_PlanDefinitionId",
                table: "PlanPricings",
                newName: "IX_PlanPricings_PlanDefinitionId");

            migrationBuilder.RenameIndex(
                name: "IX_plan_features_FeatureDefinitionId",
                table: "PlanFeatures",
                newName: "IX_PlanFeatures_FeatureDefinitionId");

            migrationBuilder.RenameIndex(
                name: "ix_feature_definitions_code",
                table: "FeatureDefinitions",
                newName: "IX_FeatureDefinitions_Code");

            migrationBuilder.RenameIndex(
                name: "ix_audit_logs_user_id_timestamp",
                table: "AuditLogs",
                newName: "IX_AuditLogs_UserId_Timestamp");

            migrationBuilder.RenameIndex(
                name: "ix_audit_logs_timestamp",
                table: "AuditLogs",
                newName: "IX_AuditLogs_Timestamp");

            migrationBuilder.RenameIndex(
                name: "ix_audit_logs_ip_address_timestamp",
                table: "AuditLogs",
                newName: "IX_AuditLogs_IpAddress_Timestamp");

            migrationBuilder.RenameIndex(
                name: "ix_audit_logs_action_timestamp",
                table: "AuditLogs",
                newName: "IX_AuditLogs_Action_Timestamp");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanPricings",
                table: "PlanPricings",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanFeatures",
                table: "PlanFeatures",
                columns: new[] { "PlanDefinitionId", "FeatureDefinitionId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_PlanDefinitions",
                table: "PlanDefinitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeatureDefinitions",
                table: "FeatureDefinitions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AuditLogs",
                table: "AuditLogs",
                column: "Id");

            migrationBuilder.UpdateData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f1111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f2222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f3333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "FeatureDefinitions",
                keyColumn: "Id",
                keyValue: new Guid("f4444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_users_UserId",
                table: "AuditLogs",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanFeatures_FeatureDefinitions_FeatureDefinitionId",
                table: "PlanFeatures",
                column: "FeatureDefinitionId",
                principalTable: "FeatureDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanFeatures_PlanDefinitions_PlanDefinitionId",
                table: "PlanFeatures",
                column: "PlanDefinitionId",
                principalTable: "PlanDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanPricings_PlanDefinitions_PlanDefinitionId",
                table: "PlanPricings",
                column: "PlanDefinitionId",
                principalTable: "PlanDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_PlanDefinitions_plan_definition_id",
                table: "subscriptions",
                column: "plan_definition_id",
                principalTable: "PlanDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_PlanPricings_plan_pricing_id",
                table: "subscriptions",
                column: "plan_pricing_id",
                principalTable: "PlanPricings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
