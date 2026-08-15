namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>検証結果。エラー一覧と有効フラグを保持します。</summary>
public sealed class ValidationResult
{
    /// <summary>検出されたエラーメッセージの一覧（<see cref="Issues"/> の Message）。</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>安定 <c>code</c> 付きの指摘。旧経路で文字列だけ渡した場合は <see cref="ValidationIssue.RuleId"/> が空。</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    /// <summary>エラーが 0 件の場合 true。</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// 検証で収集したエラーメッセージで結果を構築する。
    /// </summary>
    /// <param name="errors">エラーメッセージの一覧。</param>
    public ValidationResult(IReadOnlyList<string> errors)
        : this(errors.Select(static m => new ValidationIssue(string.Empty, m)).ToArray())
    {
    }

    /// <summary>
    /// 指摘一覧から結果を構築する。
    /// </summary>
    /// <param name="issues">指摘。</param>
    public ValidationResult(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues;
        Errors = issues.Select(static i => i.Message).ToArray();
    }
}
