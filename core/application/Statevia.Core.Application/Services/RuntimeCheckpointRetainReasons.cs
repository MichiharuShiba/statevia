namespace Statevia.Core.Application.Services;

/// <summary>
/// runtime checkpoint <b>内容</b>を保持する理由（寿命表）。
/// </summary>
/// <remarks>
/// <para>
/// 破棄判定の正本は <see cref="RuntimeCheckpointLifetimePolicy"/>。
/// 理由が空で Running のときだけ内容破棄候補になる。
/// </para>
/// <para>
/// Worker 所有 lease（in-process ownership / DB owner 列）は本列挙に含めない。
/// 所有中の Delete 抑止は <c>DiscardOrRefreshRuntimeCheckpointAsync</c> 側の別関心である。
/// </para>
/// </remarks>
[Flags]
internal enum RuntimeCheckpointRetainReasons
{
    /// <summary>保持理由なし（Running なら内容破棄候補）。</summary>
    None = 0,

    /// <summary>グラフ上に Engine durable Wait がある。</summary>
    PendingWaits = 1 << 0,

    /// <summary>親に未終端の物理分岐（<c>execution_branches.status=Running</c>）がある。</summary>
    PendingPhysicalJoin = 1 << 1,
}
