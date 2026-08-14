namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// 定義検証失敗。HTTP 422 の <c>code</c> は <see cref="Issues"/> の <see cref="ValidationIssue.RuleId"/> と一致させる。
/// </summary>
public sealed class DefinitionValidationException : ArgumentException
{
    /// <summary>検証失敗を構築する。</summary>
    public DefinitionValidationException()
        : this("Definition validation failed.")
    {
    }

    /// <summary>検証失敗を構築する。</summary>
    /// <param name="message">メッセージ。</param>
    public DefinitionValidationException(string message)
        : base(message)
    {
        Issues = [];
    }

    /// <summary>検証失敗を構築する。</summary>
    /// <param name="message">メッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public DefinitionValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Issues = [];
    }

    /// <summary>検証失敗を構築する。</summary>
    /// <param name="result">失敗した検証結果。</param>
    public DefinitionValidationException(ValidationResult result)
        : base(FormatMessage(result))
    {
        ArgumentNullException.ThrowIfNull(result);
        Issues = result.Issues;
    }

    /// <summary>ルール単位の指摘。</summary>
    public IReadOnlyList<ValidationIssue> Issues { get; }

    private static string FormatMessage(ValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var firstRuleId = result.Issues.Count > 0 ? result.Issues[0].RuleId : string.Empty;
        var prefix = firstRuleId switch
        {
            var id when id.StartsWith("Structural.", StringComparison.Ordinal) =>
                "Level 1 validation failed: ",
            var id when id.StartsWith("Reachability.", StringComparison.Ordinal) =>
                "Level 2 validation failed: ",
            _ => "Definition validation failed: "
        };
        return prefix + string.Join("; ", result.Errors);
    }
}
