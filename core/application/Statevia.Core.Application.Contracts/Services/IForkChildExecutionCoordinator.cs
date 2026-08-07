using Statevia.Core.Engine.Abstractions;

namespace Statevia.Core.Application.Contracts.Services;

/// <summary>Fork 展開における 1 分岐分の写像済み入力。</summary>
/// <param name="BranchState">分岐先頭状態名。</param>
/// <param name="MappedInput">親 Context で評価済みの子 Start input（未定義時は Fork 元 output）。</param>
public sealed record ForkBranchExpansion(
    string BranchState,
    object? MappedInput);

/// <summary>Fork 物理子展開の要求。</summary>
/// <param name="ParentExecutionId">親 execution。</param>
/// <param name="TenantId">テナント。</param>
/// <param name="DefinitionId">定義 ID。</param>
/// <param name="DefinitionVersionId">定義版 ID。</param>
/// <param name="DefinitionVersion">定義版番号（Start 要求用）。</param>
/// <param name="DefinitionDisplayId">Start の definitionId に渡す表示／UUID 文字列。</param>
/// <param name="SecuritySnapshotJson">親から継承する security snapshot JSON。</param>
/// <param name="CompiledDefinition">親と同じコンパイル済み定義（Join 解決用）。</param>
/// <param name="ForkNodeId">親実行グラフ上の Fork ノード ID（到達インスタンス）。</param>
/// <param name="Branches">分岐先頭と写像済み input。</param>
public sealed record ForkExpansionRequest(
    Guid ParentExecutionId,
    Guid TenantId,
    Guid DefinitionId,
    Guid DefinitionVersionId,
    int DefinitionVersion,
    string DefinitionDisplayId,
    string SecuritySnapshotJson,
    CompiledWorkflowDefinition CompiledDefinition,
    string ForkNodeId,
    IReadOnlyList<ForkBranchExpansion> Branches);

/// <summary>Fork 物理子展開の結果。</summary>
/// <param name="Succeeded">展開が完了したとき true。上限超過で親 Failed にしたとき false。</param>
/// <param name="ChildExecutionIds">作成または再利用した子 execution ID（分岐順）。</param>
/// <param name="JoinState">解決した Join 状態名。</param>
public sealed record ForkExpansionResult(
    bool Succeeded,
    IReadOnlyList<Guid> ChildExecutionIds,
    string JoinState);

/// <summary>親 Join 再評価の結果種別。</summary>
public enum ForkJoinEvaluationKind
{
    /// <summary>未終端子があり、Join を進めない。</summary>
    Waiting,

    /// <summary>全必須子が Completed。候補 input で Join 充足可能。</summary>
    Satisfied,

    /// <summary>必須子のいずれかが Failed / Cancelled。親へ失敗伝播する。</summary>
    Failed
}

/// <summary>
/// Join 充足時に親 Context へ投影する子 1 件分の Context 断片。
/// </summary>
/// <remarks>リスト順が適用順。同一キーは後勝ち（D5）。</remarks>
/// <param name="States">子の完了済み State エントリ（stateName → `{ output: … }`）。</param>
/// <param name="Vars">子終端時点の vars オブジェクト。</param>
public sealed record ForkJoinChildContextMerge(
    IReadOnlyDictionary<string, object?> States,
    object? Vars);

/// <summary>親 Join 再評価の結果。</summary>
/// <param name="Kind">評価結果。</param>
/// <param name="JoinState">対象 Join 状態名。</param>
/// <param name="CandidateInputs">
/// <see cref="ForkJoinEvaluationKind.Satisfied"/> のとき、分岐先頭 → 子終端 output の辞書。
/// </param>
/// <param name="ContextMerges">
/// <see cref="ForkJoinEvaluationKind.Satisfied"/> のとき、UpdatedAt 昇順の Context 断片（後勝ち）。
/// </param>
/// <param name="FailureStatus">
/// <see cref="ForkJoinEvaluationKind.Failed"/> のとき、親へ伝播する終端 status
///（<c>Failed</c> または <c>Cancelled</c>）。
/// </param>
public sealed record ForkJoinEvaluation(
    ForkJoinEvaluationKind Kind,
    string JoinState,
    IReadOnlyDictionary<string, object?>? CandidateInputs = null,
    IReadOnlyList<ForkJoinChildContextMerge>? ContextMerges = null,
    string? FailureStatus = null);

/// <summary>
/// 親 ID で届いた Wait イベントを子 execution へ振り向ける配送先。
/// </summary>
/// <param name="ExecutionId">実際に Resume する execution（子）。</param>
/// <param name="NodeId">子上の Wait ノード ID。</param>
/// <param name="EventName">再開イベント名。</param>
public sealed record ForkWaitDeliveryTarget(
    Guid ExecutionId,
    string NodeId,
    string EventName);

/// <summary>
/// Hosted Runtime 向けに Fork を親＋子 execution へ展開する。
/// </summary>
/// <remarks>
/// <para>子行作成・security snapshot 継承・Start enqueue・展開リトライ（D9）を担う。</para>
/// <para>子終端信号・Join 再評価（D2-B / D3）と、充足時の親 Context（states / vars）マージ材料（D5）を担う。</para>
/// <para>親 Cancel の未終端子カスケードと、分岐 Wait の配送先解決（要件4）も担う。</para>
/// <para>input 写像は Engine 側で行い、本コーディネータは写像済み input を受け取る。</para>
/// </remarks>
public interface IForkChildExecutionCoordinator
{
    /// <summary>
    /// Fork 到達時に子 execution を作成し Start work item を enqueue する。
    /// </summary>
    /// <param name="request">展開要求。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>展開結果。</returns>
    Task<ForkExpansionResult> ExpandForkAsync(ForkExpansionRequest request, CancellationToken ct);

    /// <summary>
    /// 子 execution 終端を <c>execution_branches</c> へ反映し、親へ予約 Resume を enqueue する。
    /// </summary>
    /// <param name="childExecutionId">終端した子 execution。</param>
    /// <param name="status">子の投影 status（Completed / Failed / Cancelled）。</param>
    /// <param name="outputJson">子終端 output の JSON（任意）。</param>
    /// <param name="statesJson">子終端 <c>states</c> の JSON（任意）。</param>
    /// <param name="varsJson">子終端 <c>vars</c> の JSON（任意）。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>分岐行があり信号を処理したとき true。非子のとき false。</returns>
    Task<bool> NotifyChildTerminalAsync(
        Guid childExecutionId,
        string status,
        string? outputJson,
        string? statesJson,
        string? varsJson,
        CancellationToken ct);

    /// <summary>
    /// 親の指定 Fork 到達について <c>execution_branches</c> から Join を再評価する。
    /// </summary>
    /// <param name="parentExecutionId">親 execution。</param>
    /// <param name="forkNodeId">親実行グラフ上の Fork ノード ID。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>評価結果（Waiting / Satisfied / Failed）。</returns>
    Task<ForkJoinEvaluation> EvaluateJoinAsync(
        Guid parentExecutionId,
        string forkNodeId,
        CancellationToken ct);

    /// <summary>
    /// 親 Cancel 時に、未終端（Running）の子へ Cancel work item を enqueue する。
    /// </summary>
    /// <remarks>ネスト時は各層の Cancel 処理で再帰的に孫へ伝播する。</remarks>
    /// <param name="parentExecutionId">親（または中間親）execution。</param>
    /// <param name="ct">キャンセル トークン。</param>
    Task CascadeCancelToRunningChildrenAsync(Guid parentExecutionId, CancellationToken ct);

    /// <summary>
    /// 親 ID へ届いた Wait Resume / PublishEvent を、子の <c>execution_waits</c> へ解決する。
    /// </summary>
    /// <remarks>
    /// <para>要求 execution 自身に一致 Wait があるときは null（呼び出し側が従来どおり処理）。</para>
    /// <para>子にだけ一致が 1 件あるとき配送先を返す。複数一致は例外（422）。</para>
    /// </remarks>
    /// <param name="requestedExecutionId">クライアントが指定した execution。</param>
    /// <param name="nodeId">Resume の nodeId。PublishEvent 互換は null。</param>
    /// <param name="eventName">再開イベント名。</param>
    /// <param name="ct">キャンセル トークン。</param>
    /// <returns>子への振替先。振替不要または一致なしのとき null。</returns>
    /// <exception cref="InvalidOperationException">複数子に一致 Wait があり曖昧なとき。</exception>
    Task<ForkWaitDeliveryTarget?> TryResolveChildWaitDeliveryAsync(
        Guid requestedExecutionId,
        string? nodeId,
        string eventName,
        CancellationToken ct);
}

/// <summary>Fork 展開で Join を一意に解決できないとき。</summary>
public sealed class ForkJoinResolutionException : InvalidOperationException
{
    /// <summary>例外を生成する。</summary>
    /// <param name="message">説明。</param>
    public ForkJoinResolutionException(string message)
        : base(message)
    {
    }
}
