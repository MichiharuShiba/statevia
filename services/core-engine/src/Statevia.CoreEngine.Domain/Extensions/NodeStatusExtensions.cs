using Statevia.CoreEngine.Domain.Node;

namespace Statevia.CoreEngine.Domain.Extensions;

/// <summary>NodeStatus の拡張メソッド。</summary>
public static class NodeStatusExtensions
{
    /// <summary>未終端（進行中）の状態か。IDLE / READY / RUNNING / WAITING のいずれか。</summary>
    public static bool IsActive(this NodeStatus status) =>
        status is NodeStatus.IDLE or NodeStatus.READY or NodeStatus.RUNNING or NodeStatus.WAITING;
}
