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
