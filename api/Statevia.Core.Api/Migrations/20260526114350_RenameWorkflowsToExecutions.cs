using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Core.Api.Migrations;

/// <inheritdoc />
/// <remarks>
/// ロールバック: Down で逆 RENAME + display_ids / command_dedup を復元。
/// workflows 行は DROP せず RENAME のみ（task 7a）。
/// </remarks>
public partial class RenameWorkflowsToExecutions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE workflows RENAME TO executions;
            ALTER TABLE executions RENAME COLUMN workflow_id TO execution_id;

            ALTER TABLE event_store RENAME COLUMN workflow_id TO execution_id;
            ALTER TABLE execution_graph_snapshots RENAME COLUMN workflow_id TO execution_id;
            ALTER TABLE event_delivery_dedup RENAME COLUMN workflow_id TO execution_id;
            ALTER TABLE workflow_events RENAME COLUMN workflow_id TO execution_id;

            ALTER INDEX IF EXISTS "IX_event_delivery_dedup_tenant_id_workflow_id_batch_id"
                RENAME TO "IX_event_delivery_dedup_tenant_id_execution_id_batch_id";

            UPDATE display_ids SET kind = 'execution' WHERE kind = 'workflow';

            UPDATE command_dedup
            SET endpoint = REPLACE(endpoint, '/v1/workflows', '/v1/executions'),
                dedup_key = REPLACE(dedup_key, '/v1/workflows', '/v1/executions')
            WHERE endpoint LIKE '%/v1/workflows%'
               OR dedup_key LIKE '%/v1/workflows%';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE command_dedup
            SET endpoint = REPLACE(endpoint, '/v1/executions', '/v1/workflows'),
                dedup_key = REPLACE(dedup_key, '/v1/executions', '/v1/workflows')
            WHERE endpoint LIKE '%/v1/executions%'
               OR dedup_key LIKE '%/v1/executions%';

            UPDATE display_ids SET kind = 'workflow' WHERE kind = 'execution';

            ALTER INDEX IF EXISTS "IX_event_delivery_dedup_tenant_id_execution_id_batch_id"
                RENAME TO "IX_event_delivery_dedup_tenant_id_workflow_id_batch_id";

            ALTER TABLE workflow_events RENAME COLUMN execution_id TO workflow_id;
            ALTER TABLE event_delivery_dedup RENAME COLUMN execution_id TO workflow_id;
            ALTER TABLE execution_graph_snapshots RENAME COLUMN execution_id TO workflow_id;
            ALTER TABLE event_store RENAME COLUMN execution_id TO workflow_id;

            ALTER TABLE executions RENAME COLUMN execution_id TO workflow_id;
            ALTER TABLE executions RENAME TO workflows;
            """);
    }
}
