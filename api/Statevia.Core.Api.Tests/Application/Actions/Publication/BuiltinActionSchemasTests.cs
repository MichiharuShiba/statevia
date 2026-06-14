using Statevia.Actions.Abstractions.Publication;
using Statevia.Core.Api.Application.Actions;
using Statevia.Core.Api.Application.Actions.Publication;

namespace Statevia.Core.Api.Tests.Application.Actions.Publication;

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
        // Act
        var publication = factory(actionId);

        // Assert
        Assert.Equal(actionId, publication.Descriptor.ActionId);
        AssertSchemaId(publication.SchemaBundle.InputSchema.RootElement, actionId, isOutput: false);
        AssertSchemaId(publication.SchemaBundle.OutputSchema.RootElement, actionId, isOutput: true);
    }

    /// <summary>rest input schema は url と method を必須とする。</summary>
    [Fact]
    public void Rest_InputSchema_RequiresUrlAndMethod()
    {
        // Arrange
        var publication = BuiltinActionSchemas.Rest(WellKnownActionIds.Rest);
        var root = publication.SchemaBundle.InputSchema.RootElement;

        // Act
        var required = root.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString())
            .ToArray();

        // Assert
        Assert.Contains("url", required);
        Assert.Contains("method", required);
        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
    }

    public static TheoryData<string, Func<string, ActionPublication>> BuiltinActionCases =>
        new()
        {
            { WellKnownActionIds.NoOpCanonical, BuiltinActionSchemas.NoOp },
            { WellKnownActionIds.Sleep, BuiltinActionSchemas.Sleep },
            { WellKnownActionIds.Rest, BuiltinActionSchemas.Rest },
            { WellKnownActionIds.Notify, BuiltinActionSchemas.Notify },
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
        Assert.StartsWith($"https://statevia.dev/schemas/actions/{actionId}{segment}", id, StringComparison.Ordinal);
    }
}
