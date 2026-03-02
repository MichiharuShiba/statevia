namespace Statevia.CoreEngine.Domain.Execution;

/// <summary>実行全体の状態。data-integration-contract §2.1 および core-reducer-spec に準拠。</summary>
public enum ExecutionStatus
{
    ACTIVE = 0,
    COMPLETED = 1,
    FAILED = 2,
    CANCELED = 3,
}
