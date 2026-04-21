namespace Statevia.Core.Engine.Abstractions;

/// <summary>
/// 1 回の事実遷移における output 条件ルーティングの観測用情報（実行グラフ JSON 等へ載せる）。
/// </summary>
public sealed class ConditionRoutingDiagnostics
{
    /// <summary>評価した事実名（例: Completed）。</summary>
    public required string Fact { get; init; }

    /// <summary>
    /// 解決結果の種別。
    /// <c>linear</c>（線形のみのコンパイル済み遷移）、<c>matched_case</c>、<c>default_fallback</c>、<c>no_transition</c>。
    /// </summary>
    public required string Resolution { get; init; }

    /// <summary>一致した case のインデックス（<see cref="Resolution"/> が <c>matched_case</c> のとき）。</summary>
    public int? MatchedCaseIndex { get; init; }

    /// <summary>評価順に並んだ各 case の結果。</summary>
    public IReadOnlyList<ConditionCaseEvaluationRecord> CaseEvaluations { get; init; } =
        Array.Empty<ConditionCaseEvaluationRecord>();

    /// <summary>
    /// 条件評価まわりのメッセージ（path 解決失敗、未サポート op 等）。既存の検証エラー配列と同様に文字列で返す。
    /// </summary>
    public IReadOnlyList<string> EvaluationErrors { get; init; } = Array.Empty<string>();
}

/// <summary>単一 case の条件評価の記録。</summary>
public sealed class ConditionCaseEvaluationRecord
{
    /// <summary><c>cases</c> 配列上の 0 始まりインデックス。</summary>
    public int CaseIndex { get; init; }

    /// <summary>定義上の宣言順（タイブレーク用）。</summary>
    public int DeclarationIndex { get; init; }

    /// <summary>定義の <c>order</c>。未指定は null。</summary>
    public int? Order { get; init; }

    /// <summary>この case の条件が真だったか。</summary>
    public bool Matched { get; init; }

    /// <summary>偽のときの理由コード（例: <c>compare_unsupported</c>）。</summary>
    public string? ReasonCode { get; init; }

    /// <summary>人間向け補足（path 警告の理由など）。</summary>
    public string? ReasonDetail { get; init; }
}
