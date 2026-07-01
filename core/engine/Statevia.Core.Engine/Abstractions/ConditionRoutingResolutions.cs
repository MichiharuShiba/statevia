namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// <see cref="ConditionRoutingDiagnostics.Resolution"/> に用いる固定値（実行グラフ JSON の <c>resolution</c> と一致）。
/// </summary>
public static class ConditionRoutingResolutions
{
    /// <summary>線形のみのコンパイル済み遷移。</summary>
    public const string Linear = "linear";

    /// <summary>条件 case のいずれかが一致した。</summary>
    public const string MatchedCase = "matched_case";

    /// <summary>いずれの case も一致せず default へ。</summary>
    public const string DefaultFallback = "default_fallback";

    /// <summary>一致する case も default もなく遷移なし。</summary>
    public const string NoTransition = "no_transition";
}
