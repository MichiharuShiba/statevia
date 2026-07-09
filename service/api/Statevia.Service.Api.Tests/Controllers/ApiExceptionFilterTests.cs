using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging.Abstractions;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Versioning;
using Statevia.Service.Api.Contracts;

namespace Statevia.Service.Api.Tests.Controllers;

public sealed class ApiExceptionFilterTests
{
    private static ApiExceptionFilter CreateFilter() =>
        new(NullLogger<ApiExceptionFilter>.Instance);

    private static ExceptionContext CreateContext(DefaultHttpContext http, Exception ex)
    {
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor());
        var context = new ExceptionContext(actionContext, []);
        context.Exception = ex;
        return context;
    }

    /// <summary>
    /// NotFoundExceptionを404のエラーレスポンスに変換する。
    /// </summary>
    [Fact]
    public void OnException_MapsNotFoundExceptionTo404()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = CreateFilter();
        var ctx = CreateContext(http, new NotFoundException("missing"));

        // Act
        filter.OnException(ctx);

        // Assert
        Assert.True(ctx.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal("NOT_FOUND", payload.Error.Code);
    }

    /// <summary>
    /// ArgumentExceptionを422の検証エラーとして返す。
    /// </summary>
    [Fact]
    public void OnException_MapsArgumentExceptionTo422()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = CreateFilter();
        var ctx = CreateContext(http, new ArgumentException("bad arg"));

        // Act
        filter.OnException(ctx);

        // Assert
        Assert.True(ctx.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal("VALIDATION_ERROR", payload.Error.Code);
    }

    /// <summary>
    /// ApiValidationException を details 付き 422 として返す。
    /// </summary>
    [Fact]
    public void OnException_MapsApiValidationExceptionTo422WithDetails()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = CreateFilter();
        var details = new[] { new { message = "yaml invalid", field = "yaml" } };
        var ctx = CreateContext(http, new ApiValidationException("validation failed", details));

        // Act
        filter.OnException(ctx);

        // Assert
        Assert.True(ctx.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal("VALIDATION_ERROR", payload.Error.Code);
        Assert.NotNull(payload.Error.Details);
    }

    /// <summary>IdempotencyConflictException を 409 に写像する。</summary>
    [Fact]
    public void OnException_MapsIdempotencyConflictExceptionTo409()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = CreateFilter();
        var ctx = CreateContext(http, new IdempotencyConflictException());

        // Act
        filter.OnException(ctx);

        // Assert
        Assert.True(ctx.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal("IDEMPOTENCY_KEY_CONFLICT", payload.Error.Code);
    }

    /// <summary>StateConflictException を 409 に写像する。</summary>
    [Fact]
    public void OnException_MapsStateConflictExceptionTo409()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = CreateFilter();
        var ctx = CreateContext(http, new StateConflictException("not deleted"));

        // Act
        filter.OnException(ctx);

        // Assert
        Assert.True(ctx.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal("STATE_CONFLICT", payload.Error.Code);
    }

    /// <summary>パラメータ無し NotFoundException を 404 に写像する。</summary>
    [Fact]
    public void OnException_MapsDefaultNotFoundExceptionTo404()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = CreateFilter();
        var ctx = CreateContext(http, new NotFoundException());

        // Act
        filter.OnException(ctx);

        // Assert
        Assert.True(ctx.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    /// <summary>DefinitionMigrationRequiredException を DEFINITION_MIGRATION_REQUIRED（422）に写像する。</summary>
    [Fact]
    public void OnException_MapsDefinitionMigrationRequiredExceptionTo422()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = CreateFilter();
        var ctx = CreateContext(http, new DefinitionMigrationRequiredException("recompile required"));

        // Act
        filter.OnException(ctx);

        // Assert
        Assert.True(ctx.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal(DefinitionMigrationRequiredException.ErrorCode, payload.Error.Code);
    }

    /// <summary>
    /// その他の例外を500の内部エラーとして返す。
    /// </summary>
    [Fact]
    public void OnException_MapsOtherExceptionTo500()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = CreateFilter();
        var wrapped = new Exception("outer", new InvalidOperationException("inner"));
        var ctx = CreateContext(http, wrapped);

        // Act
        filter.OnException(ctx);

        // Assert
        Assert.True(ctx.ExceptionHandled);
        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var payload = Assert.IsType<ErrorResponse>(result.Value);
        Assert.Equal("INTERNAL_ERROR", payload.Error.Code);
    }
}

