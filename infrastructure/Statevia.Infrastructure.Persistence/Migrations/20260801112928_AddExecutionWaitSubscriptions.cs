using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionWaitSubscriptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_execution_waits_correlation_key_topic",
                table: "execution_waits");

            migrationBuilder.DropColumn(
                name: "correlation_key",
                table: "execution_waits");

            migrationBuilder.DropColumn(
                name: "topic",
                table: "execution_waits");

            migrationBuilder.CreateTable(
                name: "execution_wait_subscriptions",
                columns: table => new
                {
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    topic = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    correlation_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    resume_event_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_wait_subscriptions", x => x.subscription_id);
                    table.ForeignKey(
                        name: "FK_execution_wait_subscriptions_execution_waits_execution_id_n~",
                        columns: x => new { x.execution_id, x.node_id },
                        principalTable: "execution_waits",
                        principalColumns: new[] { "execution_id", "node_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_wait_subscriptions_execution_id_node_id",
                table: "execution_wait_subscriptions",
                columns: new[] { "execution_id", "node_id" });

            migrationBuilder.CreateIndex(
                name: "IX_execution_wait_subscriptions_topic_correlation_key",
                table: "execution_wait_subscriptions",
                columns: new[] { "topic", "correlation_key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_wait_subscriptions");

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

            migrationBuilder.CreateIndex(
                name: "IX_execution_waits_correlation_key_topic",
                table: "execution_waits",
                columns: new[] { "correlation_key", "topic" });
        }
    }
}
