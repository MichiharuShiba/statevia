namespace Statevia.Service.Api.Contracts;

/// <summary>データ連携契約に基づく HTTP エラーレスポンス。{ "error": { "code", "message", "details" } }。</summary>
public sealed class ErrorResponse
{
    /// <summary>エラー本体。</summary>
    public ApiError Error { get; init; } = null!;
}

/// <summary>契約の error オブジェクト。</summary>
public sealed class ApiError
{
    /// <summary>機械可読なエラーコード。</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>人が読む説明メッセージ。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>追加の構造化詳細（任意）。</summary>
    public object? Details { get; init; }
}
