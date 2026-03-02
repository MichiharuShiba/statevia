namespace Statevia.CoreEngine.Domain.Node;

/// <summary>ノードの状態。data-integration-contract §2.1 および core-reducer-spec に準拠。</summary>
public enum NodeStatus
{
    IDLE = 0,
    READY = 1,
    RUNNING = 2,
    WAITING = 3,
    SUCCEEDED = 4,
    FAILED = 5,
    CANCELED = 6,
}
