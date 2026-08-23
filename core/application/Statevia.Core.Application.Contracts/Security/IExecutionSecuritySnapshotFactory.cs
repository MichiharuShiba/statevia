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

    /// <summary>
    /// 親実行のスナップショットを継承し、子定義の project 文脈だけ再評価する。
    /// Live の <c>PrincipalId</c> は読まない（Worker 文脈向け）。
    /// </summary>
    /// <param name="parentSnapshot">親実行のセキュリティスナップショット。</param>
    /// <param name="tenantId">テナント内部 ID（親 snapshot と一致すること）。</param>
    /// <param name="childDefinitionId">子として開始する定義 UUID。</param>
    /// <param name="parentDefinitionId">親実行の定義 UUID（祖先集合へ積む）。</param>
    /// <param name="capturedAt">子実行永続化 tx のコミット時刻（UTC）。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>継承した子用スナップショット。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parentSnapshot"/> が null。</exception>
    /// <exception cref="InvalidOperationException">親 snapshot のテナントが一致しない。</exception>
    /// <exception cref="NotFoundException">子定義が無い、または project 実行権が無い。</exception>
    Task<ExecutionSecuritySnapshot> CaptureForChildWorkflowAsync(
        ExecutionSecuritySnapshot parentSnapshot,
        Guid tenantId,
        Guid childDefinitionId,
        Guid parentDefinitionId,
        DateTime capturedAt,
        CancellationToken cancellationToken);
}
