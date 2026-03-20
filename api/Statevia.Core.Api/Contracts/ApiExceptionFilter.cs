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

        ObjectResult result;
        if (root is NotFoundException nf)
        {
            result = ApiErrorResult.NotFound(nf.Message);
        }
        else if (root is ArgumentException arg)
        {
            result = ApiErrorResult.ValidationError(arg.Message);
        }
        else
        {
            result = new ObjectResult(new ErrorResponse
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

        context.Result = result;
        context.ExceptionHandled = true;
    }
}

