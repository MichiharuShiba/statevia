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

/// <summary>
/// Hosted Runtime 向けに Fork を親＋子 execution へ展開する。
/// </summary>
/// <remarks>
/// <para>子行作成・security snapshot 継承・Start enqueue・展開リトライ（D9）を担う。</para>
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
