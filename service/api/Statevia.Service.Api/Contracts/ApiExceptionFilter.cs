using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Statevia.Core.Application.Contracts;

namespace Statevia.Service.Api.Contracts;

/// <summary>
/// 例外から契約エラー（404 / 422 / 500）への写像を一箇所に集約するためのフィルター。
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    /// <summary>新しいインスタンスを初期化する。</summary>
    /// <param name="logger">ロガー。</param>
    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) => _logger = logger;

    /// <summary>
    /// 未処理例外を <see cref="ExceptionContext.Result"/> に変換する。
    /// </summary>
    /// <param name="context">例外コンテキスト。</param>
    public void OnException(ExceptionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ex = context.Exception;
        var root = ex;
        while (root.InnerException is { } inner)
            root = inner;

        var result = root switch
        {
            ApiValidationException validation => ApiErrorResult.ValidationError(validation.Message, validation.Details),
            NotFoundException nf => ApiErrorResult.NotFound(nf.Message),
            UnauthorizedException unauthorized => ApiErrorResult.Unauthorized(unauthorized.Code, unauthorized.Message),
            ForbiddenException forbidden => ApiErrorResult.Forbidden(forbidden.Code, forbidden.Message),
            IdempotencyConflictException idem => ApiErrorResult.Conflict("IDEMPOTENCY_KEY_CONFLICT", idem.Message),
            ArgumentException arg => ApiErrorResult.ValidationError(arg.Message),
            _ => LogInternalError(ex)
        };

        context.Result = result;
        context.ExceptionHandled = true;
    }

    private ObjectResult LogInternalError(Exception exception)
    {
        _logger.UnhandledApiException(exception);
        return new ObjectResult(new ErrorResponse
        {
            Error = new ApiError
            {
                Code = "INTERNAL_ERROR",
                Message = "Internal server error"
            }
        })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
    }
}
