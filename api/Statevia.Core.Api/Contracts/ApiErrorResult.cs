using Microsoft.AspNetCore.Mvc;

namespace Statevia.Core.Api.Contracts;

/// <summary>データ連携契約に沿ったエラー応答を返すヘルパー。404 / 422 / 409。</summary>
public static class ApiErrorResult
{
    /// <summary>404 Not Found の <see cref="ObjectResult"/> を生成する。</summary>
    /// <param name="message">クライアント向けメッセージ。</param>
    /// <returns>NOT_FOUND のエラーボディ。</returns>
    public static ObjectResult NotFound(string message = "Resource not found")
        => new(new ErrorResponse { Error = new ApiError { Code = "NOT_FOUND", Message = message } })
        { StatusCode = StatusCodes.Status404NotFound };

    /// <summary>422 Unprocessable Entity の <see cref="ObjectResult"/> を生成する。</summary>
    /// <param name="message">検証エラーメッセージ。</param>
    /// <param name="details">フィールド別エラー等（任意）。</param>
    /// <returns>VALIDATION_ERROR のエラーボディ。</returns>
    public static ObjectResult ValidationError(string message, object? details = null)
        => new(new ErrorResponse { Error = new ApiError { Code = "VALIDATION_ERROR", Message = message, Details = details } })
        { StatusCode = StatusCodes.Status422UnprocessableEntity };

    /// <summary>401 Unauthorized の <see cref="ObjectResult"/> を生成する。</summary>
    /// <param name="code">エラーコード。</param>
    /// <param name="message">説明メッセージ。</param>
    /// <returns>指定コードのエラーボディ。</returns>
    public static ObjectResult Unauthorized(string code, string message)
        => new(new ErrorResponse { Error = new ApiError { Code = code, Message = message } })
        { StatusCode = StatusCodes.Status401Unauthorized };

    /// <summary>403 Forbidden の <see cref="ObjectResult"/> を生成する。</summary>
    /// <param name="code">エラーコード。</param>
    /// <param name="message">説明メッセージ。</param>
    /// <returns>指定コードのエラーボディ。</returns>
    public static ObjectResult Forbidden(string code, string message)
        => new(new ErrorResponse { Error = new ApiError { Code = code, Message = message } })
        { StatusCode = StatusCodes.Status403Forbidden };

    /// <summary>409 Conflict の <see cref="ObjectResult"/> を生成する。</summary>
    /// <param name="code">衝突種別のコード。</param>
    /// <param name="message">説明メッセージ。</param>
    /// <param name="details">追加詳細（任意）。</param>
    /// <returns>指定コードのエラーボディ。</returns>
    public static ObjectResult Conflict(string code, string message, object? details = null)
        => new(new ErrorResponse { Error = new ApiError { Code = code, Message = message, Details = details } })
        { StatusCode = StatusCodes.Status409Conflict };
}
