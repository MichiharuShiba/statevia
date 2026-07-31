using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionWorkItemsAndWaitRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "correlation_key",
                table: "execution_waits",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "topic",
                table: "execution_waits",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "execution_work_items",
                columns: table => new
                {
                    work_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    available_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    lease_owner = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_work_items", x => x.work_item_id);
                    table.ForeignKey(
                        name: "FK_execution_work_items_executions_execution_id",
                        column: x => x.execution_id,
                        principalTable: "executions",
                        principalColumn: "execution_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_waits_correlation_key_topic",
                table: "execution_waits",
                columns: new[] { "correlation_key", "topic" });

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_available_at_lease_until",
                table: "execution_work_items",
                columns: new[] { "available_at", "lease_until" });

            migrationBuilder.CreateIndex(
                name: "IX_execution_work_items_execution_id",
                table: "execution_work_items",
                column: "execution_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_work_items");

            migrationBuilder.DropIndex(
                name: "IX_execution_waits_correlation_key_topic",
                table: "execution_waits");

            migrationBuilder.DropColumn(
                name: "correlation_key",
                table: "execution_waits");

            migrationBuilder.DropColumn(
                name: "topic",
                table: "execution_waits");
        }
    }
}
