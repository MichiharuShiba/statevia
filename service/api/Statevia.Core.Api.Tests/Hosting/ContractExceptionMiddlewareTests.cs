using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Statevia.Core.Api.Contracts;
using Statevia.Core.Api.Hosting;

namespace Statevia.Core.Api.Tests.Hosting;

/// <summary><see cref="ContractExceptionMiddleware"/> の契約例外写像。</summary>
public sealed class ContractExceptionMiddlewareTests
{
    /// <summary>UnauthorizedException は 401 JSON エラーになる。</summary>
    [Fact]
    public async Task InvokeAsync_UnauthorizedException_Returns401Json()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = new ContractExceptionMiddleware(_ => throw new UnauthorizedException("denied", "UNAUTHORIZED"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        var payload = await ReadErrorResponseAsync(context);
        Assert.Equal("UNAUTHORIZED", payload.Error.Code);
        Assert.Equal("denied", payload.Error.Message);
    }

    /// <summary>ForbiddenException は 403 JSON エラーになる。</summary>
    [Fact]
    public async Task InvokeAsync_ForbiddenException_Returns403Json()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = new ContractExceptionMiddleware(_ => throw new ForbiddenException("blocked", "TENANT_SUSPENDED"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        var payload = await ReadErrorResponseAsync(context);
        Assert.Equal("TENANT_SUSPENDED", payload.Error.Code);
        Assert.Equal("blocked", payload.Error.Message);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ErrorResponse> ReadErrorResponseAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return (await JsonSerializer.DeserializeAsync<ErrorResponse>(
            context.Response.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))!;
    }
}
