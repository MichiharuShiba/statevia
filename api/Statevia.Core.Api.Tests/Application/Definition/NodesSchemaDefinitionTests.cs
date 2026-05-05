using System.Text.Json;
using Statevia.Core.Api.Application.Definition;

namespace Statevia.Core.Api.Tests.Application.Definition;

/// <summary>
/// <see cref="NodesSchemaDefinition"/> の UI 向けスキーマ形状の退行防止。
/// </summary>
public sealed class NodesSchemaDefinitionTests
{
    /// <summary>
    /// <see cref="NodesSchemaDefinition.CreateSchemaDocument"/> の JSON に
    /// <c>workflow.properties.description</c> および
    /// <c>nodes.items.properties.input</c>（<c>anyOf</c> が 2 要素以上）が含まれること。
    /// </summary>
    [Fact]
    public void CreateSchemaDocument_IncludesWorkflowDescriptionAndNodeInput()
    {
        // Arrange
        // 追加の前提なし。静的ファクトリの戻り JSON 形状を検証する。

        // Act
        var serializedSchema = JsonSerializer.Serialize(NodesSchemaDefinition.CreateSchemaDocument());
        using var jsonDocument = JsonDocument.Parse(serializedSchema);

        // Assert
        var rootProperties = jsonDocument.RootElement.GetProperty("properties");

        Assert.True(
            rootProperties.GetProperty("workflow").GetProperty("properties").TryGetProperty("description", out _),
            "workflow.properties.description");

        Assert.True(
            rootProperties.GetProperty("nodes").GetProperty("items").GetProperty("properties").TryGetProperty("input", out var inputProperty),
            "nodes.items.properties.input");

        Assert.True(
            inputProperty.TryGetProperty("anyOf", out var anyOfArray) && anyOfArray.GetArrayLength() >= 2,
            "nodes.items.properties.input.anyOf must have at least 2 items");
    }
}
