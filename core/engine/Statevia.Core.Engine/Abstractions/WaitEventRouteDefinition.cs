namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// Wait イベント 1 件分のコンパイル済みルート（遷移先）。
/// </summary>
/// <remarks>
/// Phase 2+ で <c>Label</c> / <c>OutputSchema</c> を拡張予定。公開 DSL に <c>exit</c> 語彙は出さない。
/// </remarks>
public sealed class WaitEventRouteDefinition
{
    /// <summary>イベント発生時の遷移先状態名。</summary>
    public required string Next { get; init; }
}


/// <summary>
/// Wait 状態のコンパイル済み購読エントリ（topic / key → 内部 resume イベント）。
/// </summary>
/// <remarks>
/// <para><see cref="Key"/> は正規化済み（未指定は空文字）。NULL は使わない。</para>
/// </remarks>
public sealed class WaitSubscriptionDefinition
{
    /// <summary>購読トピック。</summary>
    public required string Topic { get; init; }

    /// <summary>相関キー（未指定は <c>""</c>）。</summary>
    public required string Key { get; init; }

    /// <summary>Resume に渡す内部イベント名（例: <c>statevia.event.subscribe.0</c>）。</summary>
    public required string ResumeEventName { get; init; }

    /// <summary>遷移先状態名。</summary>
    public required string Next { get; init; }
}

/// <summary>Subscribe モードの内部イベント名。</summary>
/// <remarks>
/// <para>プラットフォーム予約の名前空間は <see cref="ReservedNamespacePrefix"/>。</para>
/// </remarks>
public static class WaitSubscribeEventNames
{
    /// <summary>プラットフォーム予約イベント名の名前空間（利用者定義との衝突回避）。</summary>
    public const string ReservedNamespacePrefix = "statevia.event.";

    /// <summary>Subscribe 内部イベント名のプレフィックス。</summary>
    public const string Prefix = ReservedNamespacePrefix + "subscribe.";

    /// <summary>エントリ index から内部イベント名を生成する。</summary>
    /// <param name="index">0 始まりの購読エントリ index。</param>
    /// <returns>内部イベント名。</returns>
    public static string ForIndex(int index) =>
        Prefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
