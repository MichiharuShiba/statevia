using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Core.Api.Migrations
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "command_dedup",
                columns: table => new
                {
                    dedup_key = table.Column<string>(type: "text", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_command_dedup", x => x.dedup_key);
                });

            migrationBuilder.CreateTable(
                name: "display_ids",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_id = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_display_ids", x => new { x.kind, x.resource_id });
                });

            migrationBuilder.CreateTable(
                name: "event_delivery_dedup",
                columns: table => new
                {
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    accepted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_delivery_dedup", x => new { x.tenant_id, x.workflow_id, x.client_event_id });
                });

            migrationBuilder.CreateTable(
                name: "event_store",
                columns: table => new
                {
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<long>(type: "bigint", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    actor_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    schema_version = table.Column<int>(type: "integer", nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_store", x => new { x.workflow_id, x.seq });
                });

            migrationBuilder.CreateTable(
                name: "execution_graph_snapshots",
                columns: table => new
                {
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    graph_json = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_graph_snapshots", x => x.workflow_id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_definitions",
                columns: table => new
                {
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_yaml = table.Column<string>(type: "text", nullable: false),
                    compiled_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_definitions", x => x.definition_id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_events",
                columns: table => new
                {
                    workflow_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seq = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_events", x => x.workflow_event_id);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancel_requested = table.Column<bool>(type: "boolean", nullable: false),
                    restart_lost = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.workflow_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_display_ids_display_id",
                table: "display_ids",
                column: "display_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_event_delivery_dedup_tenant_id_workflow_id_batch_id",
                table: "event_delivery_dedup",
                columns: new[] { "tenant_id", "workflow_id", "batch_id" });

            migrationBuilder.CreateIndex(
                name: "IX_event_store_event_id",
                table: "event_store",
                column: "event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropTable(
                name: "command_dedup");

            migrationBuilder.DropTable(
                name: "display_ids");

            migrationBuilder.DropTable(
                name: "event_delivery_dedup");

            migrationBuilder.DropTable(
                name: "event_store");

            migrationBuilder.DropTable(
                name: "execution_graph_snapshots");

            migrationBuilder.DropTable(
                name: "workflow_definitions");

            migrationBuilder.DropTable(
                name: "workflow_events");

            migrationBuilder.DropTable(
                name: "workflows");
        }
    }
}
