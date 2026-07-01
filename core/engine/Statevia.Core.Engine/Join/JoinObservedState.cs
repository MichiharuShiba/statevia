namespace Statevia.Core.Engine.Join;

/// <summary>
/// Join 判定のために保持する、依存状態の観測結果。
/// </summary>
public sealed record JoinObservedState(string Fact, object? Output);
