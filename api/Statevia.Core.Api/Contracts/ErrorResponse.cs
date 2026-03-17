namespace Statevia.Core.Api.Contracts;

/// <summary>契約 §7 のエラーレスポンス。{ "error": { "code", "message", "details" } }。</summary>
public sealed class ErrorResponse
{
    public ApiError Error { get; init; } = null!;
}

/// <summary>契約の error オブジェクト。</summary>
public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public object? Details { get; init; }
}
