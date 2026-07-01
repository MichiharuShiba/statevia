using System.Text.Json;
using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Hosting;

/// <summary>ミドルウェア境界の契約例外を JSON エラー封筒に変換する。</summary>
internal sealed class ContractExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public ContractExceptionMiddleware(RequestDelegate next) => _next = next;

    /// <summary>リクエストパイプラインを実行する。</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (UnauthorizedException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, ex.Code, ex.Message)
                .ConfigureAwait(false);
        }
        catch (ForbiddenException ex)
        {
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, ex.Code, ex.Message)
                .ConfigureAwait(false);
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        if (context.Response.HasStarted)
            throw new InvalidOperationException("Response already started.");

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var body = new ErrorResponse { Error = new ApiError { Code = code, Message = message } };
        await JsonSerializer.SerializeAsync(context.Response.Body, body, JsonOptions, context.RequestAborted)
            .ConfigureAwait(false);
    }
}
