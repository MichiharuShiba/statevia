using System.Text.Json;
using Statevia.Service.Api.Application.Actions.Validation;
using Statevia.Core.Engine.Definition;

namespace Statevia.Service.Api.Tests.Application.Actions.Validation;

/// <summary><see cref="ActionInputTreeNormalizer"/> のマージと衝突検出。</summary>
public sealed class ActionInputTreeNormalizerTests
{
    private const string NestedShipSchemaJson = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "ship": {
              "type": "object",
              "required": ["address"],
              "additionalProperties": false,
              "properties": {
                "address": {
                  "type": "string",
                  "x-statevia-valueKind": "literalOrPath"
                },
                "contact": {
                  "type": "object",
                  "additionalProperties": false,
                  "properties": {
                    "email": {
                      "type": "string",
                      "format": "email"
                    }
                  }
                }
              }
            }
          }
        }
        """;

    /// <summary>ドットキーとネスト map が同等の論理ツリーになる。</summary>
    [Fact]
    public void Validate_DottedKeys_AndNestedMap_AreEquivalent()
    {
        // Arrange
        var schema = ParseSchema(NestedShipSchemaJson);
        var dottedInput = CreateValuesInput(
            ("ship.address", "東京都"),
            ("ship.contact.email", "a@example.com"));
        var nestedInput = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["ship"] = new()
                {
                    Literal = new Dictionary<string, object?>
                    {
                        ["address"] = "東京都",
                        ["contact"] = new Dictionary<string, object?>
                        {
                            ["email"] = "a@example.com",
                        },
                    },
                },
            },
        };

        // Act
        var dottedErrors = ActionInputSchemaValidator.Validate("Ship", "test.nested", dottedInput, schema);
        var nestedErrors = ActionInputSchemaValidator.Validate("Ship", "test.nested", nestedInput, schema);

        // Assert
        Assert.Empty(dottedErrors);
        Assert.Empty(nestedErrors);
    }

    /// <summary>ネスト必須欠落で階層 jsonPath の 422 になる。</summary>
    [Fact]
    public void Validate_MissingNestedRequired_ReturnsHierarchicalJsonPath()
    {
        // Arrange
        var schema = ParseSchema(NestedShipSchemaJson);
        var input = CreateValuesInput(("ship.contact.email", "a@example.com"));

        // Act
        var errors = ActionInputSchemaValidator.Validate("Ship", "test.nested", input, schema);

        // Assert
        Assert.Contains(errors, error => error.JsonPath == "$.input.ship.address");
    }

    /// <summary>オブジェクトリテラルとドットキー子の競合で 422 になる。</summary>
    [Fact]
    public void Validate_ObjectLiteralAndDottedChildConflict_ReturnsNormalizationError()
    {
        // Arrange
        var schema = ParseSchema(NestedShipSchemaJson);
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["ship"] = new() { Literal = "scalar-conflict" },
                ["ship.address"] = new() { Literal = "東京都" },
            },
        };

        // Act
        var errors = ActionInputSchemaValidator.Validate("Ship", "test.nested", input, schema);

        // Assert
        Assert.Contains(
            errors,
            error => error.JsonPath == "$.input.ship"
                && error.Message.Contains("conflict", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>混在 path 式をネスト階層で検証する。</summary>
    [Fact]
    public void Validate_NestedPathExpression_Passes()
    {
        // Arrange
        var schema = ParseSchema(NestedShipSchemaJson);
        var input = CreateValuesInput(
            ("ship.address", "東京都"),
            ("ship.contact.email", "$.payload.email"));

        // Act
        var errors = ActionInputSchemaValidator.Validate("Ship", "test.nested", input, schema);

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>ネスト map 内のドットキーも正規化される。</summary>
    [Fact]
    public void Normalize_DottedKeyInsideNestedMap_MergesIntoTree()
    {
        // Arrange
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["ship"] = new()
                {
                    Literal = new Dictionary<string, object?>
                    {
                        ["contact.email"] = "a@example.com",
                    },
                },
            },
        };

        // Act
        var (root, errors) = ActionInputTreeNormalizer.Normalize(input.Values);
        var children = Assert.IsType<Dictionary<string, ActionInputTreeNormalizer.NormalizedInputNode>>(root.Children);
        var ship = children["ship"];
        var shipChildren = Assert.IsType<Dictionary<string, ActionInputTreeNormalizer.NormalizedInputNode>>(ship.Children);
        var contact = shipChildren["contact"];
        var contactChildren = Assert.IsType<Dictionary<string, ActionInputTreeNormalizer.NormalizedInputNode>>(contact.Children);

        // Assert
        Assert.Empty(errors);
        Assert.NotNull(contactChildren["email"].Leaf);
        Assert.Equal("a@example.com", contactChildren["email"].Leaf!.Literal);
    }

    private static StateInputDefinition CreateValuesInput(params (string Key, string Value)[] entries)
    {
        var values = entries.ToDictionary(
            entry => entry.Key,
            entry => new StateInputValueDefinition
            {
                Literal = entry.Value.StartsWith('$') ? null : entry.Value,
                Path = entry.Value.StartsWith('$') ? entry.Value : null,
            },
            StringComparer.OrdinalIgnoreCase);
        return new StateInputDefinition { Values = values };
    }

    private static JsonElement ParseSchema(string json) =>
        JsonDocument.Parse(json).RootElement;
}
