namespace Statevia.Service.Api.Application.Actions.Validation;

/// <summary>action input schema 検証失敗時に DefinitionCompiler が送出する例外。</summary>
internal sealed class ActionInputSchemaValidationException : ArgumentException
{
    private const string DefaultMessage = "Action input schema validation failed.";

    /// <summary>シリアル化・フレームワーク互換用の既定コンストラクター。</summary>
    public ActionInputSchemaValidationException()
        : this(DefaultMessage)
    {
    }

    /// <summary>メッセージのみで生成する（構造化エラーなし）。</summary>
    /// <param name="message">エラーメッセージ。</param>
    public ActionInputSchemaValidationException(string message)
        : base(message)
    {
        Errors = [];
    }

    /// <summary>メッセージと内部例外で生成する（構造化エラーなし）。</summary>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="innerException">内部例外。</param>
    public ActionInputSchemaValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
        Errors = [];
    }

    /// <summary>検証エラー一覧を保持して生成する。</summary>
    /// <param name="errors">1 件以上の検証エラー。</param>
    public ActionInputSchemaValidationException(IReadOnlyList<ActionInputValidationError> errors)
        : base(BuildMessage(errors))
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("At least one validation error is required.", nameof(errors));
        }

        Errors = errors;
    }

    /// <summary>構造化検証エラー。標準コンストラクター経由では空。</summary>
    public IReadOnlyList<ActionInputValidationError> Errors { get; }

    private static string BuildMessage(IReadOnlyList<ActionInputValidationError> errors) =>
        string.Join(
            "; ",
            errors.Select(error =>
                $"state '{error.State}' action '{error.ActionId}' at {error.JsonPath}: {error.Message}"));
}
