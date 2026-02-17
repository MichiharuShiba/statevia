namespace Statevia.Core.FSM;

/// <summary>
/// FSM の遷移評価結果。next / fork / end のいずれかを表します。
/// </summary>
public sealed class TransitionResult
{
    /// <summary>遷移が存在するか。</summary>
    public bool HasTransition { get; init; }
    /// <summary>次に遷移する状態名（next 遷移）。</summary>
    public string? Next { get; init; }
    /// <summary>Fork で並列開始する状態の一覧。</summary>
    public IReadOnlyList<string>? Fork { get; init; }
    /// <summary>ワークフロー終了かどうか。</summary>
    public bool End { get; init; }

    public static TransitionResult None => new() { HasTransition = false };
    public static TransitionResult ToNext(string next) => new() { HasTransition = true, Next = next };
    public static TransitionResult ToFork(IReadOnlyList<string> fork) => new() { HasTransition = true, Fork = fork };
    public static TransitionResult ToEnd() => new() { HasTransition = true, End = true };
}
