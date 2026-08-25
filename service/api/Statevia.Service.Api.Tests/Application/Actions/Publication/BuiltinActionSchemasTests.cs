using Statevia.Core.Actions.Abstractions.Publication;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Publication;

namespace Statevia.Service.Api.Tests.Application.Actions.Publication;

/// <summary><see cref="BuiltinActionSchemas"/> の Builtin schema 定義検証。</summary>
public sealed class BuiltinActionSchemasTests
{
    /// <summary>全 Builtin action に $id 付き input/output schema が定義されている。</summary>
    [Theory]
    [MemberData(nameof(BuiltinActionCases))]
    public void BuiltinSchemas_DefineInputAndOutputWithSchemaId(
        string actionId,
        Func<string, ActionPublication> factory)
    {
        // Arrange
        ArgumentNullException.ThrowIfNull(factory);

        // Act
        var publication = factory(actionId);

        // Assert
        Assert.Equal(actionId, publication.Descriptor.ActionId);
        AssertSchemaId(publication.SchemaBundle.InputSchema.RootElement, actionId, isOutput: false);
        AssertSchemaId(publication.SchemaBundle.OutputSchema.RootElement, actionId, isOutput: true);
    }

    /// <summary>sleep input schema は duration を必須とする。</summary>
    [Fact]
    public void Sleep_InputSchema_RequiresDuration()
    {
        // Arrange
        var publication = BuiltinActionSchemas.Sleep(WellKnownActionIds.Sleep);
        var root = publication.SchemaBundle.InputSchema.RootElement;

        // Act
        var required = root.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();

        // Assert
        Assert.Contains("duration", required);
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
    }

    /// <summary>workflow input schema は definitionId のみ必須で mode / timeout を持たない。</summary>
    [Fact]
    public void Workflow_InputSchema_RequiresDefinitionIdOnly()
    {
        // Arrange
        var publication = BuiltinActionSchemas.Workflow(WellKnownActionIds.Workflow);
        var root = publication.SchemaBundle.InputSchema.RootElement;

        // Act
        var required = root.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        var properties = root.GetProperty("properties");

        // Assert
        Assert.Equal(["definitionId"], required);
        Assert.True(properties.TryGetProperty("definitionId", out _));
        Assert.True(properties.TryGetProperty("input", out _));
        Assert.False(properties.TryGetProperty("mode", out _));
        Assert.False(properties.TryGetProperty("timeout", out _));
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
    }

    /// <summary>publish input schema は topic 必須で key 任意、遷移名フィールドは置かない。</summary>
    [Fact]
    public void Publish_InputSchema_RequiresTopicAndOptionalKey()
    {
        // Arrange
        var publication = BuiltinActionSchemas.Publish(WellKnownActionIds.Publish);
        var root = publication.SchemaBundle.InputSchema.RootElement;

        // Act
        var required = root.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();
        var properties = root.GetProperty("properties");

        // Assert
        Assert.Equal(["topic"], required);
        Assert.True(properties.TryGetProperty("topic", out _));
        Assert.True(properties.TryGetProperty("key", out _));
        Assert.True(properties.TryGetProperty("payload", out _));
        Assert.False(properties.TryGetProperty("event", out _));
        Assert.Equal(["topic", "key", "payload"], publication.UiMetadata!.FieldOrder);
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
    }

    public static TheoryData<string, Func<string, ActionPublication>> BuiltinActionCases =>
        new()
        {
            { WellKnownActionIds.NoOpCanonical, BuiltinActionSchemas.NoOp },
            { WellKnownActionIds.Sleep, BuiltinActionSchemas.Sleep },
            { WellKnownActionIds.Signal, BuiltinActionSchemas.Signal },
            { WellKnownActionIds.Publish, BuiltinActionSchemas.Publish },
            { WellKnownActionIds.Workflow, BuiltinActionSchemas.Workflow },
        };

    private static void AssertSchemaId(
        System.Text.Json.JsonElement root,
        string actionId,
        bool isOutput)
    {
        var segment = isOutput ? "/output" : "/input";
        var id = root.GetProperty("$id").GetString();
        Assert.StartsWith(
            $"{StateviaActionSchemaVocabulary.ActionSchemaIdBaseUri}/{actionId}{segment}",
            id,
            StringComparison.Ordinal);
    }
}
