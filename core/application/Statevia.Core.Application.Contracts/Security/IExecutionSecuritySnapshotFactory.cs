namespace Statevia.Core.Application.Contracts.Security;

/// <summary>Start 時点の <see cref="ExecutionSecuritySnapshot"/> を構築する。</summary>
public interface IExecutionSecuritySnapshotFactory
{
    /// <summary>
    /// 現在の Principal と定義文脈から Start 用スナップショットを構築する。
    /// Identity（Live）と global permission（Live）は呼び出し前に検証済みであること。
    /// </summary>
    /// <param name="tenantId">テナント内部 ID。</param>
    /// <param name="definitionId">開始対象定義 ID。</param>
    /// <param name="capturedAt">永続化 tx のコミット時刻（UTC）。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    Task<ExecutionSecuritySnapshot> CaptureForStartAsync(
        Guid tenantId,
        Guid definitionId,
        DateTime capturedAt,
        CancellationToken cancellationToken);
}
