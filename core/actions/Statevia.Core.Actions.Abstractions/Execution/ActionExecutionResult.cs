using System.Text.Json;

namespace Statevia.Core.Actions.Abstractions.Execution;

/// <summary>Platform 実行層からの Action 実行結果。</summary>
public sealed record ActionExecutionResult
{
    /// <summary>成功したか。</summary>
    public bool Success { get; init; }

    /// <summary>出力（OutOfProcess 向け JSON、任意）。</summary>
    public JsonElement? Output { get; init; }

    /// <summary>InProcess 実行の出力（Engine へ返す生オブジェクト、任意）。</summary>
    public object? RuntimeOutput { get; init; }

    /// <summary>エラーコード（任意）。</summary>
    public string? ErrorCode { get; init; }

    /// <summary>エラーメッセージ（任意・機微値を含めない）。</summary>
    public string? ErrorMessage { get; init; }
}
