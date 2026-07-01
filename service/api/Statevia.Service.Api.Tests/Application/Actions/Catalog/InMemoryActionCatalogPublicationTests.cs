using System.Text.Json;
using Statevia.Actions.Abstractions.Catalog;
using Statevia.Actions.Abstractions.Publication;
using Statevia.Service.Api.Application.Actions.Builtins;
using Statevia.Service.Api.Application.Actions.Catalog;
using Statevia.Core.Engine.Execution;
using CatalogActionDescriptor = Statevia.Actions.Abstractions.Catalog.ActionDescriptor;
using PublicationActionDescriptor = Statevia.Actions.Abstractions.Publication.ActionDescriptor;

namespace Statevia.Service.Api.Tests.Application.Actions.Catalog;

/// <summary><see cref="InMemoryActionCatalog"/> の ActionPublication 登録・取得検証。</summary>
public sealed class InMemoryActionCatalogPublicationTests
{
    private const string TestActionId = "statevia.action.builtin.test";

    /// <summary>Publication 付き登録後に TryGetPublication で取得できる。</summary>
    [Fact]
    public void Register_WithPublication_TryGetPublication_ReturnsSchema()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var publication = CreatePublication(TestActionId);

        // Act
        catalog.Register(CreateCatalogDescriptor(TestActionId), CreateEntry(), publication);

        // Assert
        Assert.True(catalog.TryGetPublication(TestActionId, out var resolved));
        Assert.NotNull(resolved);
        Assert.Equal(TestActionId, resolved!.Descriptor.ActionId);
        Assert.Equal("Test Action", resolved.Descriptor.DisplayName);
        Assert.Equal("2020-12", resolved.SchemaBundle.SchemaVersion);
    }

    /// <summary>Publication なし登録は TryGetPublication で false。</summary>
    [Fact]
    public void Register_WithoutPublication_TryGetPublication_ReturnsFalse()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();

        // Act
        catalog.Register(CreateCatalogDescriptor(TestActionId), CreateEntry());

        // Assert
        Assert.False(catalog.TryGetPublication(TestActionId, out _));
    }

    /// <summary>不正 labelKey プレフィックスは登録時に ArgumentException。</summary>
    [Fact]
    public void Register_WithInvalidLabelKeyPrefix_Throws()
    {
        // Arrange
        var catalog = new InMemoryActionCatalog();
        var publication = CreatePublication(TestActionId) with
        {
            UiMetadata = new ActionUiMetadata(
                Fields: new Dictionary<string, ActionFieldUiHints>
                {
                    ["url"] = new ActionFieldUiHints(LabelKey: "other.action.ui.fields.url.label"),
                }),
        };

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            catalog.Register(CreateCatalogDescriptor(TestActionId), CreateEntry(), publication));
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

    private static ActionCatalogEntry CreateEntry() =>
        new(InProcessFactory: _ => DefaultStateExecutor.Create(new NoOpState()));

    private static ActionPublication CreatePublication(string actionId)
    {
        using var inputSchema = JsonDocument.Parse("""{"type":"object","properties":{"url":{"type":"string"}}}""");
        using var outputSchema = JsonDocument.Parse("""{"type":"object","properties":{"statusCode":{"type":"integer"}}}""");
        return new ActionPublication(
            new PublicationActionDescriptor(
                actionId,
                "1.0.0",
                "Test Action",
                Category: "Http"),
            new ActionSchemaBundle(
                JsonDocument.Parse(inputSchema.RootElement.GetRawText()),
                JsonDocument.Parse(outputSchema.RootElement.GetRawText())),
            new ActionUiMetadata(
                FieldOrder: ["url"],
                Fields: new Dictionary<string, ActionFieldUiHints>
                {
                    ["url"] = new ActionFieldUiHints(
                        Widget: "url",
                        LabelKey: $"{actionId}.ui.fields.url.label"),
                }));
    }
}
