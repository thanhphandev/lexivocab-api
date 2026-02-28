using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LexiVocab.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RequestName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AdditionalInfo = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW() AT TIME ZONE 'UTC'"),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_Timestamp",
                table: "AuditLogs",
                columns: new[] { "Action", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_IpAddress_Timestamp",
                table: "AuditLogs",
                columns: new[] { "IpAddress", "Timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "UserId", "Timestamp" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
