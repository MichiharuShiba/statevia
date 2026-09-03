namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行系コマンド／照会入口の認可チェックを横断で提供する。
/// </summary>
/// <remarks>
/// <para>各ドメインサービスの入口から呼び、認可呼び出し箇所を一覧可能にする。</para>
/// <para>定義受理時のプロジェクト実行権と、Runtime / mutation snapshot ベースの read/write を担う。</para>
/// </remarks>
/// <param name="runtimeAuth">Runtime 権限。</param>
/// <param name="mutationAuth">実行ミューテーション認可。</param>
/// <param name="projectAuth">プロジェクト認可。</param>
/// <param name="definitions">定義リポジトリ（プロジェクト解決）。</param>
/// <param name="executor">トランザクション実行。</param>
internal sealed class ExecutionAuthorizationGuard(
    IRuntimePermissionAuthorization runtimeAuth,
    IExecutionMutationAuthorization mutationAuth,
    IProjectAuthorizationService projectAuth,
    IDefinitionRepository definitions,
    ICoreTransactionExecutor executor)
{
    /// <summary>Runtime の executions:read を要求する。</summary>
    /// <param name="ct">キャンセル。</param>
    public Task EnsureExecutionsReadAsync(CancellationToken ct) =>
        runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsRead, ct);

    /// <summary>Runtime の executions:write を要求する。</summary>
    /// <param name="ct">キャンセル。</param>
    public Task EnsureExecutionsWriteAsync(CancellationToken ct) =>
        runtimeAuth.EnsurePermissionAsync(RuntimePermissionRequirements.ExecutionsWrite, ct);

    /// <summary>実行行の security snapshot に基づきミューテーション権限を要求する。</summary>
    /// <param name="execution">実行投影行。</param>
    /// <param name="ct">キャンセル。</param>
    public Task EnsureExecutionMutationWriteAsync(ExecutionRow execution, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(execution);
        var snapshot = ExecutionSecuritySnapshotJson.TryDeserialize(execution.SecuritySnapshotJson);
        return mutationAuth.EnsureMutationPermissionAsync(
            snapshot,
            RuntimePermissionRequirements.ExecutionsWrite,
            ct);
    }

    /// <summary>定義が属するプロジェクト上で実行開始可能かを要求する。</summary>
    /// <param name="tenantId">テナント ID。</param>
    /// <param name="definitionId">定義 ID。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="NotFoundException">定義が見つからない、または論理削除済みのとき。</exception>
    public Task EnsureCanExecuteOnDefinitionAsync(
        Guid tenantId,
        Guid definitionId,
        CancellationToken ct) =>
        executor.ExecuteReadOnlyAsync(
            async (uow, innerCt) =>
            {
                var projectId = await definitions
                    .ResolveProjectIdAsync(uow, tenantId, definitionId, innerCt)
                    .ConfigureAwait(false);
                if (projectId is null)
                    throw new NotFoundException(ExecutionValidationMessages.DefinitionNotFound);

                await projectAuth
                    .EnsureCanExecuteAsync(uow, tenantId, projectId.Value, innerCt)
                    .ConfigureAwait(false);
            },
            ct);
}
