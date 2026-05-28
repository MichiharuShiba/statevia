using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Core.Api.Migrations;

/// <inheritdoc />
/// <remarks>
/// wait_kind 語彙を EventWait / CallbackWait / DelayWait に統一（旧 HumanWait 等からの改名）。
/// </remarks>
public partial class RenameExecutionWaitKindValues : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE execution_waits SET wait_kind = 'EventWait' WHERE wait_kind = 'HumanWait';
            UPDATE execution_waits SET wait_kind = 'CallbackWait' WHERE wait_kind = 'ExternalCallbackWait';
            UPDATE execution_waits SET wait_kind = 'DelayWait' WHERE wait_kind = 'LongDelayWait';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE execution_waits SET wait_kind = 'HumanWait' WHERE wait_kind = 'EventWait';
            UPDATE execution_waits SET wait_kind = 'ExternalCallbackWait' WHERE wait_kind = 'CallbackWait';
            UPDATE execution_waits SET wait_kind = 'LongDelayWait' WHERE wait_kind = 'DelayWait';
            """);
    }
}
