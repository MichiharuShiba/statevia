namespace Statevia.Core.Application.Contracts.Services;

/// <summary>
/// Hosted Runtime が Fork を物理子 execution に展開するときの試行上限（D9）。
/// </summary>
/// <remarks>
/// <para>設定セクション例: <c>Statevia:Runtime:ForkChildExpansion</c>。</para>
/// <para>一時失敗では親をすぐ Failed にせず再試行し、上限超過後に親 Failed（残子 Cancel）とする。</para>
/// </remarks>
public sealed class ForkChildExpansionOptions
{
    /// <summary>構成バインド用セクション名。</summary>
    public const string SectionName = "Statevia:Runtime:ForkChildExpansion";

    /// <summary>展開操作の最大試行回数（初回含む）。既定 5。</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>試行間の基準遅延（ミリ秒）。既定 100。</summary>
    public int BaseDelayMs { get; set; } = 100;
}
