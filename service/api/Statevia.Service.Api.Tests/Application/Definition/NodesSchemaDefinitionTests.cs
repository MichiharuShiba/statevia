using System.Text.Json;
using Statevia.Service.Api.Application.Definition;

namespace Statevia.Service.Api.Tests.Application.Definition;

/// <summary>
/// <see cref="NodesSchemaDefinition"/> の UI 向けスキーマ形状の退行防止。
/// </summary>
public sealed class NodesSchemaDefinitionTests
{
    /// <summary>
    /// <see cref="NodesSchemaDefinition.CreateSchemaDocument"/> の JSON に
    /// <c>workflow.properties.description</c>、
    /// <c>nodes.items.properties.input</c>（<c>anyOf</c> が 2 要素以上）、
    /// および <c>nodes.items.required</c> に <c>name</c>/<c>type</c>（<c>id</c> なし）が含まれること。
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

        var nodeRequired = rootProperties.GetProperty("nodes").GetProperty("items").GetProperty("required");
        var requiredKeys = nodeRequired.EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("name", requiredKeys);
        Assert.Contains("type", requiredKeys);
        Assert.DoesNotContain("id", requiredKeys);
    }

    /// <summary>
    /// <see cref="NodesSchemaDefinition.CreateSchemaDocument"/> の JSON に
    /// <c>nodes.items.properties.error</c>（<c>oneOf</c> が string 型と object 型の 2 要素）が含まれること。
    /// </summary>
    [Fact]
    public void CreateSchemaDocument_IncludesNodeErrorWithOneOf()
    {
        // Arrange
        // 追加の前提なし。静的ファクトリの戻り JSON 形状を検証する。

        // Act
        var serializedSchema = JsonSerializer.Serialize(NodesSchemaDefinition.CreateSchemaDocument());
        using var jsonDocument = JsonDocument.Parse(serializedSchema);

        // Assert
        var nodeProperties = jsonDocument.RootElement
            .GetProperty("properties")
            .GetProperty("nodes")
            .GetProperty("items")
            .GetProperty("properties");

        Assert.True(
            nodeProperties.TryGetProperty("error", out var errorProperty),
            "nodes.items.properties.error が存在すること");

        Assert.True(
            errorProperty.TryGetProperty("oneOf", out var oneOfArray) && oneOfArray.GetArrayLength() >= 2,
            "nodes.items.properties.error.oneOf が 2 要素以上であること");

        var hasStringType = false;
        var hasObjectWithNameProperty = false;

        foreach (var oneOfItem in oneOfArray.EnumerateArray())
        {
            if (oneOfItem.TryGetProperty("type", out var typeElement))
            {
                if (typeElement.GetString() is "string")
                {
                    hasStringType = true;
                }
                else if (typeElement.GetString() is "object"
                    && oneOfItem.TryGetProperty("properties", out var props)
                    && props.TryGetProperty("name", out _))
                {
                    hasObjectWithNameProperty = true;
                }
            }
        }

        Assert.True(hasStringType, "error.oneOf に type:string の要素が含まれること");
        Assert.True(hasObjectWithNameProperty, "error.oneOf に type:object かつ properties.name を持つ要素が含まれること");
    }
}
