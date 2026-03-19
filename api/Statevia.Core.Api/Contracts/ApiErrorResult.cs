using Microsoft.AspNetCore.Mvc;

namespace Statevia.Core.Api.Contracts;

/// <summary>契約 §7 に沿ったエラー応答を返すヘルパー。404 / 422 / 409。</summary>
public static class ApiErrorResult
{
    public static ObjectResult NotFound(string message = "Resource not found")
        => new(new ErrorResponse { Error = new ApiError { Code = "NOT_FOUND", Message = message } })
        { StatusCode = StatusCodes.Status404NotFound };

    public static ObjectResult ValidationError(string message, object? details = null)
        => new(new ErrorResponse { Error = new ApiError { Code = "VALIDATION_ERROR", Message = message, Details = details } })
        { StatusCode = StatusCodes.Status422UnprocessableEntity };

    public static ObjectResult Conflict(string code, string message, object? details = null)
        => new(new ErrorResponse { Error = new ApiError { Code = code, Message = message, Details = details } })
        { StatusCode = StatusCodes.Status409Conflict };
}
