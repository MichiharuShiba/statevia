using System.Text.Json;
using Statevia.Core.Actions.Abstractions.Catalog;
using Statevia.Core.Actions.Abstractions.Publication;
using Statevia.Core.Application.Services;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Service.Api.Contracts;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Core.Engine.Execution;
using CatalogActionDescriptor = Statevia.Core.Actions.Abstractions.Catalog.ActionDescriptor;
using PublicationActionDescriptor = Statevia.Core.Actions.Abstractions.Publication.ActionDescriptor;

namespace Statevia.Service.Api.Tests.Services;

/// <summary><see cref="ActionSchemaService"/> の DTO 分割と 404 検証。</summary>
public sealed class ActionSchemaServiceTests
{
    private const string TestActionId = "statevia.action.builtin.test";

    /// <summary>一覧 API が publication 付き action の descriptor 概要を返す。</summary>
    [Fact]
    public void GetList_ReturnsDescriptorSummary()
    {
        // Arrange
        var catalog = CreateCatalogWithPublication();
        var service = new ActionSchemaService(catalog);

        // Act
        var response = service.GetList();

        // Assert
        var item = Assert.Single(response.Items);
        Assert.Equal(TestActionId, item.ActionId);
        Assert.Equal("Test Action", item.DisplayName);
        Assert.True(item.HasSchema);
    }

    /// <summary>index API が publication 付き action の軽量項目を返す。</summary>
    [Fact]
    public void GetIndex_ReturnsLightweightItems()
    {
        // Arrange
        var catalog = CreateCatalogWithPublication();
        var service = new ActionSchemaService(catalog);

        // Act
        var response = service.GetIndex();

        // Assert
        var item = Assert.Single(response.Items);
        Assert.Equal(TestActionId, item.ActionId);
        Assert.Equal("Test Action", item.DisplayName);
        Assert.Equal("1.0.0", item.Version);
    }

    /// <summary>詳細 API が schema / metadata / descriptor を分離 DTO で返す。</summary>
    [Fact]
    public void GetDetail_ReturnsSeparatedDto()
    {
        // Arrange
        var catalog = CreateCatalogWithPublication();
        var service = new ActionSchemaService(catalog);

        // Act
        var response = service.GetDetail(TestActionId);

        // Assert
        Assert.Equal(TestActionId, response.Descriptor.ActionId);
        Assert.Equal("Test Action", response.Descriptor.DisplayName);
        Assert.Equal("2020-12", response.Schema.SchemaVersion);
        Assert.Equal(JsonValueKind.Object, response.Schema.InputSchema.ValueKind);
        Assert.NotNull(response.UiMetadata);
        Assert.Contains("url", response.UiMetadata!.Fields!.Keys, StringComparer.Ordinal);
    }

    /// <summary>未登録 actionId は NotFoundException。</summary>
    [Fact]
    public void GetDetail_WhenActionMissing_ThrowsNotFound()
    {
        // Arrange
        var service = new ActionSchemaService(new InMemoryActionCatalog());

        // Act / Assert
        Assert.Throws<NotFoundException>(() => service.GetDetail("missing.action"));
    }

    /// <summary>publication 未登録 action は詳細で NotFoundException。</summary>
    [Fact]
    public void GetDetail_WhenPublicationMissing_ThrowsNotFound()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        catalog.Register(
            CreateCatalogDescriptor(TestActionId),
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new NoOpState())));
        var service = new ActionSchemaService(catalog);

        // Act / Assert
        Assert.Throws<NotFoundException>(() => service.GetDetail(TestActionId));
    }

    private static InMemoryActionCatalog CreateCatalogWithPublication()
    {
        var catalog = new InMemoryActionCatalog();
        catalog.Register(
            CreateCatalogDescriptor(TestActionId),
            new ActionCatalogEntry(InProcessFactory: _ => DefaultStateExecutor.Create(new NoOpState())),
            CreatePublication(TestActionId));
        return catalog;
    }

    private static CatalogActionDescriptor CreateCatalogDescriptor(string actionId) =>
        new()
        {
            ActionId = actionId,
            ModuleId = "statevia.builtin",
            Version = "1.0.0",
            TrustLevel = ActionTrustLevel.Trusted,
            Source = ActionSourceKind.Builtin,
            Visibility = ActionVisibility.Builtin,
        };

    private static ActionPublication CreatePublication(string actionId) =>
        new(
            new PublicationActionDescriptor(actionId, "1.0.0", "Test Action", Category: "Test"),
            new ActionSchemaBundle(
                JsonDocument.Parse("""{"type":"object","properties":{"url":{"type":"string"}}}"""),
                JsonDocument.Parse("""{"type":"object"}""")),
            new ActionUiMetadata(
                FieldOrder: ["url"],
                Fields: new Dictionary<string, ActionFieldUiHints>
                {
                    ["url"] = new ActionFieldUiHints(
                        Widget: "url",
                        LabelKey: $"{actionId}.ui.fields.url.label"),
                }));
}
