using Microsoft.AspNetCore.Mvc;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Controllers;
using Statevia.Service.Api.Tests.Infrastructure;

namespace Statevia.Service.Api.Tests.Controllers;

/// <summary><see cref="ActionSchemasController"/> の 200/404 応答検証。</summary>
public sealed class ActionSchemasControllerTests
{
    private sealed class FakeActionSchemaService : IActionSchemaService
    {
        public Exception? DetailException { get; init; }

        public ActionSchemaListResponse GetList() =>
            new()
            {
                Items =
                [
                    new ActionSchemaListItemDto
                    {
                        ActionId = "statevia.action.builtin.rest",
                        DisplayName = "REST",
                        Version = "1.0.0",
                        HasSchema = true,
                    },
                ],
            };

        public ActionSchemaIndexResponse GetIndex() =>
            new()
            {
                Items =
                [
                    new ActionSchemaIndexItemDto
                    {
                        ActionId = "statevia.action.builtin.rest",
                        DisplayName = "REST",
                        Version = "1.0.0",
                    },
                ],
            };

        public ActionSchemaDetailResponse GetDetail(string actionId)
        {
            if (DetailException is not null)
            {
                throw DetailException;
            }

            return new ActionSchemaDetailResponse
            {
                Descriptor = new ActionSchemaDescriptorDto
                {
                    ActionId = actionId,
                    DisplayName = "REST",
                    Version = "1.0.0",
                },
            };
        }
    }

    /// <summary>一覧 API が 200 と items を返す。</summary>
    [Fact]
    public async Task GetList_ReturnsItems()
    {
        // Arrange
        var controller = new ActionSchemasController(
            new FakeActionSchemaService(),
            new AllowAllRuntimePermissionAuthorization());

        // Act
        var res = await controller.GetList(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<ActionSchemaListResponse>(ok.Value);
        Assert.Single(body.Items);
    }

    /// <summary>index API が 200 と items を返す。</summary>
    [Fact]
    public async Task GetIndex_ReturnsItems()
    {
        // Arrange
        var controller = new ActionSchemasController(
            new FakeActionSchemaService(),
            new AllowAllRuntimePermissionAuthorization());

        // Act
        var res = await controller.GetIndex(CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<ActionSchemaIndexResponse>(ok.Value);
        Assert.Single(body.Items);
    }

    /// <summary>詳細 API が 200 と schema DTO を返す。</summary>
    [Fact]
    public async Task GetDetail_ReturnsDetail()
    {
        // Arrange
        var controller = new ActionSchemasController(
            new FakeActionSchemaService(),
            new AllowAllRuntimePermissionAuthorization());

        // Act
        var res = await controller.GetDetail("statevia.action.builtin.rest", CancellationToken.None);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(res.Result);
        var body = Assert.IsType<ActionSchemaDetailResponse>(ok.Value);
        Assert.Equal("statevia.action.builtin.rest", body.Descriptor.ActionId);
    }

    /// <summary>未登録 actionId は NotFoundException を伝播する。</summary>
    [Fact]
    public async Task GetDetail_WhenMissing_ThrowsNotFound()
    {
        // Arrange
        var controller = new ActionSchemasController(
            new FakeActionSchemaService { DetailException = new NotFoundException("missing") },
            new AllowAllRuntimePermissionAuthorization());

        // Act / Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.GetDetail("missing.action", CancellationToken.None));
    }
}
