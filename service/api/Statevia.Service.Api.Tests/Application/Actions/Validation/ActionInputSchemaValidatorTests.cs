using Statevia.Core.Engine.Definition;
using Statevia.Service.Api.Application.Actions;
using Statevia.Service.Api.Application.Actions.Publication;
using Statevia.Service.Api.Application.Actions.Validation;
using System.Text.Json;

namespace Statevia.Service.Api.Tests.Application.Actions.Validation;

/// <summary><see cref="ActionInputSchemaValidator"/> の valueKind と型検証。</summary>
public sealed class ActionInputSchemaValidatorTests
{
    private const string StateName = "Send";
    private const string ActionId = WellKnownActionIds.Sleep;

    /// <summary>literalOrPath フィールドにリテラル文字列を指定すると型検証が適用される。</summary>
    [Fact]
    public void Validate_LiteralOrPathField_WithStringLiteral_PassesEnumCheck()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "method": {
                  "type": "string",
                  "enum": ["GET", "POST"],
                  "x-statevia-valueKind": "literalOrPath"
                }
              }
            }
            """);
        var input = CreateValuesInput(("method", "GET"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>literalOrPath フィールドに SimpleJsonPath を指定すると型検証をスキップする。</summary>
    [Fact]
    public void Validate_LiteralOrPathField_WithPathExpression_SkipsTypeCheck()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "url": {
                  "type": "string",
                  "x-statevia-valueKind": "literalOrPath"
                }
              }
            }
            """);
        var input = CreateValuesInput(("url", "$.payload.endpoint"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>literal フィールドに JSONPath を指定すると 422 相当のエラーになる。</summary>
    [Fact]
    public void Validate_LiteralField_WithPathExpression_ReturnsError()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "name": {
                  "type": "string",
                  "x-statevia-valueKind": "literal"
                }
              }
            }
            """);
        var input = CreateValuesInput(("name", "$.payload.name"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Single(errors);
        Assert.Equal("$.input.name", errors[0].JsonPath);
        Assert.Contains("not allowed", errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>path フィールドには JSONPath 式が必須。</summary>
    [Fact]
    public void Validate_PathField_WithLiteral_ReturnsError()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "ref": {
                  "type": "string",
                  "x-statevia-valueKind": "path"
                }
              }
            }
            """);
        var input = CreateValuesInput(("ref", "static"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Single(errors);
        Assert.Equal("$.input.ref", errors[0].JsonPath);
        Assert.Contains("JSONPath", errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>必須フィールド欠落でエラーを返す。</summary>
    [Fact]
    public void Validate_MissingRequiredField_ReturnsError()
    {
        // Arrange
        var schema = RestLikeSchema();
        var input = CreateValuesInput(("method", "GET"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Contains(errors, error => error.JsonPath == "$.input.url");
    }

    /// <summary>additionalProperties false で未知プロパティを拒否する。</summary>
    [Fact]
    public void Validate_UnknownProperty_WhenAdditionalPropertiesFalse_ReturnsError()
    {
        // Arrange
        var schema = RestLikeSchema();
        var input = CreateValuesInput(
            ("url", "https://example.test"),
            ("method", "GET"),
            ("unexpected", "x"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Contains(errors, error => error.JsonPath == "$.input.unexpected");
    }

    /// <summary>input.path 単一形式は schema 検証の対象外。</summary>
    [Fact]
    public void Validate_PathOnlyInput_SkipsSchemaValidation()
    {
        // Arrange
        var schema = RestLikeSchema();
        var input = new StateInputDefinition { Path = "$.payload" };

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>sleep duration の oneOf（整数リテラル）を受理する。</summary>
    [Fact]
    public void Validate_SleepDurationIntegerLiteral_PassesOneOf()
    {
        // Arrange
        var schema = BuiltinActionSchemas.Sleep(WellKnownActionIds.Sleep).SchemaBundle.InputSchema.RootElement;
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["duration"] = new() { Literal = 5000 },
            },
        };

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, WellKnownActionIds.Sleep, input, schema);

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>enum 不一致でエラーを返す。</summary>
    [Fact]
    public void Validate_InvalidEnumValue_ReturnsTypeError()
    {
        // Arrange
        var schema = RestLikeSchema();
        var input = CreateValuesInput(
            ("url", "https://example.test"),
            ("method", "INVALID"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Contains(errors, error => error.JsonPath == "$.input.method");
    }

    /// <summary>format uri 不一致でエラーを返す。</summary>
    [Fact]
    public void Validate_InvalidUriFormat_ReturnsTypeError()
    {
        // Arrange
        var schema = RestLikeSchema();
        var input = CreateValuesInput(
            ("url", "not-a-uri"),
            ("method", "GET"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Contains(errors, error => error.JsonPath == "$.input.url");
    }

    /// <summary>integer minimum 未満でエラーを返す。</summary>
    [Fact]
    public void Validate_IntegerBelowMinimum_ReturnsTypeError()
    {
        // Arrange
        var schema = RestLikeSchema();
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["url"] = new() { Literal = "https://example.test" },
                ["method"] = new() { Literal = "GET" },
                ["timeout"] = new() { Literal = 0 },
            },
        };

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Contains(errors, error => error.JsonPath == "$.input.timeout");
    }

    /// <summary>無効な JSONPath 式でエラーを返す。</summary>
    [Fact]
    public void Validate_InvalidPathExpression_ReturnsError()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "url": {
                  "type": "string",
                  "x-statevia-valueKind": "literalOrPath"
                }
              }
            }
            """);
        var input = CreateValuesInput(("url", "$.bad-"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Single(errors);
        Assert.Equal("$.input.url", errors[0].JsonPath);
    }

    /// <summary>headers オブジェクトリテラルを受理する。</summary>
    [Fact]
    public void Validate_ObjectLiteralHeaders_Passes()
    {
        // Arrange
        var schema = RestLikeSchema();
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["url"] = new() { Literal = "https://example.test" },
                ["method"] = new() { Literal = "GET" },
                ["headers"] = new()
                {
                    Literal = new Dictionary<string, object?>
                    {
                        ["Authorization"] = "Bearer token",
                    },
                },
            },
        };

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>noop は additionalProperties true のため任意 input を受理する。</summary>
    [Fact]
    public void Validate_NoOpWithExtraInput_Passes()
    {
        // Arrange
        var schema = BuiltinActionSchemas.NoOp(WellKnownActionIds.NoOpCanonical).SchemaBundle.InputSchema.RootElement;
        var input = CreateValuesInput(("custom", "value"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, WellKnownActionIds.NoOpCanonical, input, schema);

        // Assert
        Assert.Empty(errors);
    }

    /// <summary>path 専用フィールドにリテラルを渡すとエラーになる。</summary>
    [Fact]
    public void Validate_PathOnlyField_WithLiteral_ReturnsError()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "ref": {
                  "type": "string",
                  "x-statevia-valueKind": "path"
                }
              }
            }
            """);
        var input = CreateValuesInput(("ref", "plain"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Contains(errors, e => e.JsonPath.Contains("ref", StringComparison.Ordinal));
    }

    /// <summary>oneOf に一致しないリテラルはエラーになる。</summary>
    [Fact]
    public void Validate_OneOfLiteral_WhenNoVariantMatches_ReturnsError()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "mode": {
                  "oneOf": [
                    { "type": "string", "enum": ["a"] },
                    { "type": "integer" }
                  ]
                }
              }
            }
            """);
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["mode"] = new() { Literal = true }
            }
        };

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.NotEmpty(errors);
    }

    /// <summary>integer の minimum 未満はエラーになる。</summary>
    [Fact]
    public void Validate_IntegerBelowMinimum_ReturnsError()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "count": {
                  "type": "integer",
                  "minimum": 1
                }
              }
            }
            """);
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["count"] = new() { Literal = 0 }
            }
        };

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.NotEmpty(errors);
    }

    /// <summary>email format 不正は MatchesString で弾かれる。</summary>
    [Fact]
    public void Validate_EmailFormatInvalid_ReturnsError()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "to": {
                  "type": "string",
                  "format": "email"
                }
              }
            }
            """);
        var input = CreateValuesInput(("to", "@invalid"));

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.NotEmpty(errors);
    }

    /// <summary>type 配列の不一致メッセージを返す。</summary>
    [Fact]
    public void Validate_TypeArrayMismatch_ReturnsCombinedExpectedMessage()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "value": {
                  "type": ["string", "null"]
                }
              }
            }
            """);
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["value"] = new() { Literal = 42 }
            }
        };

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.Contains(errors, e => e.Message.Contains("string", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>additionalProperties false のオブジェクトリテラルにキーがあると失敗する。</summary>
    [Fact]
    public void Validate_ObjectLiteral_WithDisallowedAdditionalProperties_ReturnsError()
    {
        // Arrange
        var schema = ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "properties": {
                "meta": {
                  "type": "object",
                  "additionalProperties": false
                }
              }
            }
            """);
        var input = new StateInputDefinition
        {
            Values = new Dictionary<string, StateInputValueDefinition>
            {
                ["meta"] = new()
                {
                    Literal = new Dictionary<string, object?> { ["k"] = "v" }
                }
            }
        };

        // Act
        var errors = ActionInputSchemaValidator.Validate(StateName, ActionId, input, schema);

        // Assert
        Assert.NotEmpty(errors);
    }

    private static StateInputDefinition CreateValuesInput(params (string Key, string Value)[] entries)
    {
        var values = entries.ToDictionary(
            entry => entry.Key,
            entry => new StateInputValueDefinition
            {
                Literal = entry.Value.StartsWith('$')
                    ? null
                    : entry.Value,
                Path = entry.Value.StartsWith('$')
                    ? entry.Value
                    : null,
            },
            StringComparer.OrdinalIgnoreCase);
        return new StateInputDefinition { Values = values };
    }

    private static JsonElement RestLikeSchema() =>
        ParseSchema(
            """
            {
              "type": "object",
              "additionalProperties": false,
              "required": ["url", "method"],
              "properties": {
                "url": { "type": "string", "format": "uri" },
                "method": { "type": "string", "enum": ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"] },
                "headers": { "type": "object", "additionalProperties": { "type": "string" } },
                "timeout": { "type": "integer", "minimum": 1 }
              }
            }
            """);

    private static JsonElement ParseSchema(string json) =>
        JsonDocument.Parse(json).RootElement;
}
