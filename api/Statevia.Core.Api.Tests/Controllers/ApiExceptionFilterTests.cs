using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Statevia.Core.Api.Contracts;

namespace Statevia.Core.Api.Tests.Controllers;

public sealed class ApiExceptionFilterTests
{
    private static ExceptionContext CreateContext(DefaultHttpContext http, Exception ex)
    {
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(http, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor());
        var context = new ExceptionContext(actionContext, new List<IFilterMetadata>());
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
        var filter = new ApiExceptionFilter();
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
        var filter = new ApiExceptionFilter();
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
    /// その他の例外を500の内部エラーとして返す。
    /// </summary>
    [Fact]
    public void OnException_MapsOtherExceptionTo500()
    {
        // Arrange
        var http = new DefaultHttpContext();
        var filter = new ApiExceptionFilter();
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

