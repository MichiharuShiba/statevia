using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Core.Application.Services;

/// <summary>
/// runtime checkpoint 内容の寿命表（破棄候補判定）。
/// </summary>
/// <remarks>
/// <para>
/// 保持理由は <see cref="RuntimeCheckpointRetainReasons"/> のみ
///（<see cref="RuntimeCheckpointRetainReasons.PendingWaits"/> /
/// <see cref="RuntimeCheckpointRetainReasons.PendingPhysicalJoin"/>）。
/// </para>
/// <para>
/// Worker 所有 lease は同列にしない。所有中は内容破棄候補でも行 Delete せず
/// runtime JSON の refresh に倒す（呼び出し側）。
/// </para>
/// <para>契約の文章正本は <c>docs/concepts/durability.md</c> および
/// <c>docs/specifications/execution/fork-join.md</c>。</para>
/// </remarks>
internal static class RuntimeCheckpointLifetimePolicy
{
    /// <summary>
    /// 寿命表に従い、checkpoint <b>内容</b>を破棄してよいか。
    /// </summary>
    /// <param name="projectionStatus">投影 status（<see cref="ExecutionProjectionStatuses"/>）。</param>
    /// <param name="retainReasons">内容保持理由。終端では無視する。</param>
    /// <returns>
    /// 終端、または Running かつ保持理由が空のとき <see langword="true"/>。
    /// </returns>
    public static bool IsContentDiscardCandidate(
        string projectionStatus,
        RuntimeCheckpointRetainReasons retainReasons)
    {
        if (!string.Equals(
                projectionStatus,
                ExecutionProjectionStatuses.Running,
                StringComparison.Ordinal))
        {
            return true;
        }

        return retainReasons == RuntimeCheckpointRetainReasons.None;
    }
}
