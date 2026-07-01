using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Service.Api.Migrations;

/// <inheritdoc />
/// <remarks>
/// ロールバック: Down でテーブル名・PK 列・制約名を逆 RENAME（DROP なし）。
/// FK 列 execution_id は task 7a 済み（再変更なし）。
/// </remarks>
public partial class RenameWorkflowEventsToExecutionEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE workflow_events RENAME TO execution_events;
            ALTER TABLE execution_events RENAME COLUMN workflow_event_id TO execution_event_id;
            ALTER TABLE execution_events RENAME CONSTRAINT "PK_workflow_events" TO "PK_execution_events";
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE execution_events RENAME CONSTRAINT "PK_execution_events" TO "PK_workflow_events";
            ALTER TABLE execution_events RENAME COLUMN execution_event_id TO workflow_event_id;
            ALTER TABLE execution_events RENAME TO workflow_events;
            """);
    }
}
