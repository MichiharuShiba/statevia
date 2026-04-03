using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Statevia.Core.Api.Contracts;

/// <summary>
/// 例外 -> 契約エラー（404 / 422 / 500）への写像を一箇所に集約するためのフィルター。
/// </summary>
public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var ex = context.Exception;
        var root = ex;
        while (root.InnerException is { } inner)
            root = inner;

        var result = root switch
        {
            NotFoundException nf => ApiErrorResult.NotFound(nf.Message),
            IdempotencyConflictException idem => ApiErrorResult.Conflict("IDEMPOTENCY_KEY_CONFLICT", idem.Message),
            ArgumentException arg => ApiErrorResult.ValidationError(arg.Message),
            _ => new ObjectResult(new ErrorResponse
            {
                Error = new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "Internal server error"
                }
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            }
        };

        context.Result = result;
        context.ExceptionHandled = true;
    }
}

