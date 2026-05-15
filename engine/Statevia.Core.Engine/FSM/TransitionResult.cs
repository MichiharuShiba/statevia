namespace Statevia.Core.Engine.FSM;

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

    /// <summary>遷移が存在しない結果。</summary>
    public static TransitionResult None => new() { HasTransition = false };

    /// <summary>指定状態へ進む next 遷移の結果を返す。</summary>
    /// <param name="next">次の状態名。</param>
    public static TransitionResult ToNext(string next) => new() { HasTransition = true, Next = next };

    /// <summary>指定ブランチへ進む fork 遷移の結果を返す。</summary>
    /// <param name="fork">並列開始する状態名の一覧。</param>
    public static TransitionResult ToFork(IReadOnlyList<string> fork) => new() { HasTransition = true, Fork = fork };

    /// <summary>ワークフロー終端へ進む結果を返す。</summary>
    public static TransitionResult ToEnd() => new() { HasTransition = true, End = true };
}
