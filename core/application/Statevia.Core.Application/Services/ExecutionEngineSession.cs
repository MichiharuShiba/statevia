using System.Text.Json;
using Microsoft.Extensions.Logging;
using Statevia.Core.Application.Infrastructure;
using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Services;

/// <summary>
/// 実行 Engine の Load（checkpoint hydrate）共用ヘルパー。
/// </summary>
/// <remarks>
/// <para>Cancel / Publish / Resume / Recover / Join など複数ドメインから共有する。</para>
/// <para>Unload ゲートや Wait quiesce 待機はドメイン固有のためここに含めない（肥大化防止）。</para>
/// </remarks>
/// <param name="engine">実行エンジン。</param>
/// <param name="compiler">定義コンパイル復元。</param>
/// <param name="definitions">定義リポジトリ。</param>
/// <param name="checkpointStore">runtime checkpoint 永続化。</param>
/// <param name="executor">トランザクション実行。</param>
/// <param name="logger">構造化ログ。</param>
internal sealed class ExecutionEngineSession(
    IExecutionEngine engine,
    IDefinitionCompilerService compiler,
    IDefinitionRepository definitions,
    IExecutionCheckpointStore checkpointStore,
    ICoreTransactionExecutor executor,
    ILogger<ExecutionEngineSession> logger)
{
    /// <summary>
    /// ミューテーション前に Engine インスタンスを保証する（未ロードなら checkpoint から hydrate）。
    /// </summary>
    /// <remarks>
    /// 終端投影かつメモリ未ロード、または checkpoint 欠落・不正時は例外。二重 hydrate は 422 相当へ写像する。
    /// </remarks>
    /// <param name="executionId">実行 UUID。</param>
    /// <param name="execution">投影行（テナント・定義バージョン・status）。</param>
    /// <param name="ct">キャンセル。</param>
    /// <exception cref="ArgumentException">終端未ロード、checkpoint 欠落、または二重 hydrate。</exception>
    /// <exception cref="InvalidOperationException">定義バージョン欠落または checkpoint 不正。</exception>
    public async Task EnsureEngineRuntimeLoadedForMutationAsync(
        Guid executionId,
        ExecutionRow execution,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(execution);
        if (engine.GetSnapshot(executionId.ToString()) is not null)
            return;

        if (ExecutionProjectionStatuses.IsTerminal(execution.Status))
        {
            throw new ArgumentException(
                "The execution is already in a terminal state in the database projection, but there is no in-memory instance in this API process. Cancel or event delivery cannot be applied.");
        }

        var checkpointDocument = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => checkpointStore.GetByExecutionIdAsync(uow, executionId, innerCt),
            ct).ConfigureAwait(false);
        if (checkpointDocument is null)
        {
            throw new ArgumentException(
                "The execution state is not loaded in this API process and no runtime checkpoint was found.");
        }

        var versionRow = await executor.ExecuteReadOnlyAsync(
            (uow, innerCt) => definitions.GetVersionForExecutionByIdAsync(
                uow,
                execution.TenantId,
                execution.DefinitionVersionId,
                innerCt),
            ct).ConfigureAwait(false);
        if (versionRow is null)
        {
            throw new InvalidOperationException(
                $"Definition version '{execution.DefinitionVersionId}' was not found for execution '{executionId}'.");
        }

        var compiled = RestoreCompiledDefinitionFromVersion(versionRow);
        if (!TryParseRuntimeCheckpoint(checkpointDocument.CheckpointJson, out var checkpoint, out var parseError))
        {
            logger.RuntimeCheckpointInvalidForHydrate(
                executionId,
                checkpointDocument.CheckpointJson?.Length ?? 0);
            if (parseError is not null)
                logger.RuntimeCheckpointDeserializeFailed(parseError, executionId);

            throw new InvalidOperationException(
                $"Stored runtime checkpoint for execution '{executionId:D}' is empty or invalid and cannot be hydrated.");
        }

        try
        {
            engine.ImportCheckpoint(compiled, checkpoint);
        }
        catch (InvalidOperationException ex)
        {
            // 二重 hydrate 等はクライアント向け 422（ArgumentException）へ写像する。
            throw new ArgumentException(ex.Message, ex);
        }
    }

    /// <summary>
    /// 永続 checkpoint JSON を hydrate 用に検証・デシリアライズする。
    /// </summary>
    /// <remarks>
    /// <c>BeginOwnedSessionAsync</c> の seed <c>{}</c> や必須欠落は不正とし、Import しない。
    /// </remarks>
    /// <param name="checkpointJson">永続化された checkpoint JSON。</param>
    /// <param name="checkpoint">成功時のデシリアライズ結果。</param>
    /// <param name="parseError">JSON 例外時のみ設定。形式不正（必須欠落）は null。</param>
    /// <returns>hydrate 可能なとき true。</returns>
    internal static bool TryParseRuntimeCheckpoint(
        string? checkpointJson,
        out ExecutionRuntimeCheckpoint checkpoint,
        out Exception? parseError)
    {
        checkpoint = null!;
        parseError = null;

        if (string.IsNullOrWhiteSpace(checkpointJson))
            return false;

        var trimmed = checkpointJson.Trim();
        if (trimmed is "{}" or "null")
            return false;

        try
        {
            var parsed = JsonSerializer.Deserialize<ExecutionRuntimeCheckpoint>(
                checkpointJson,
                JsonSerializerProfiles.CamelCase);
            if (parsed is null
                || string.IsNullOrWhiteSpace(parsed.ExecutionId)
                || string.IsNullOrWhiteSpace(parsed.DefinitionName)
                || parsed.ActiveStates is null
                || parsed.Graph is null
                || parsed.Join is null
                || parsed.Context is null
                || parsed.PendingWaits is null)
            {
                return false;
            }

            // 物理 Join 待ち中は ActiveStates / PendingWaits が空の正規断面になり得る（Fork Unload 後）。
            // 空配列だけでは破損とみなさない（seed "{}" は上で拒否済み）。

            checkpoint = parsed;
            return true;
        }
        catch (JsonException ex)
        {
            parseError = ex;
            return false;
        }
    }

    /// <summary>
    /// 永続化された定義バージョンからコンパイル済み定義を復元する。
    /// </summary>
    private CompiledWorkflowDefinition RestoreCompiledDefinitionFromVersion(DefinitionVersionRow versionRow)
    {
        try
        {
            return compiler.RestoreFromStoredVersion(versionRow.SourceYaml, versionRow.CompiledJson);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(ExecutionValidationMessages.StoredDefinitionVersionInvalid, ex);
        }
    }
}
