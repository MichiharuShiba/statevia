using System.Globalization;
using System.Text.Json;
using Statevia.Actions.Abstractions.Publication;
using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Api.Application.Actions.Validation;

/// <summary>
/// action 状態の <c>input</c> map を inputSchema に対して検証する。
/// <c>input.path</c> 単一形式は L1 検証のみで本検証の対象外。
/// </summary>
internal static class ActionInputSchemaValidator
{
    private const string ValueKindKeyword = StateviaActionSchemaVocabulary.ValueKindKeyword;

    /// <summary>action 状態の input を schema に照合する。</summary>
    /// <param name="stateName">状態名。</param>
    /// <param name="actionId">canonical actionId。</param>
    /// <param name="input">状態 input 定義。</param>
    /// <param name="inputSchemaRoot">input JSON Schema ルート。</param>
    /// <returns>検出した検証エラー（空なら成功）。</returns>
    public static IReadOnlyList<ActionInputValidationError> Validate(
        string stateName,
        string actionId,
        StateInputDefinition? input,
        JsonElement inputSchemaRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);

        if (IsPathOnlyInput(input))
        {
            return [];
        }

        var values = input?.Values;
        var context = new ValidationContext(stateName, actionId);
        var (root, normalizationErrors) = ActionInputTreeNormalizer.Normalize(values);
        foreach (var error in normalizationErrors)
        {
            context.Add(error.JsonPath, error.Message);
        }

        if (normalizationErrors.Count == 0 && root.Children is Dictionary<string, ActionInputTreeNormalizer.NormalizedInputNode> children)
        {
            ValidateNormalizedObject(children, inputSchemaRoot, "$.input", context);
        }

        return context.Errors;
    }

    private static bool IsPathOnlyInput(StateInputDefinition? input) =>
        input?.Path is not null && (input.Values is null || input.Values.Count == 0);

    private static void ValidateNormalizedObject(
        IReadOnlyDictionary<string, ActionInputTreeNormalizer.NormalizedInputNode> children,
        JsonElement schema,
        string jsonPath,
        ValidationContext context)
    {
        var properties = TryGetProperties(schema, out var propertyMap);
        var additionalPropertiesAllowed = ResolveAdditionalProperties(schema);
        var required = ResolveRequired(schema);
        var presentKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, node) in children)
        {
            presentKeys.Add(key);
            var fieldPath = $"{jsonPath}.{key}";
            if (!properties || !propertyMap!.TryGetValue(key, out var propertySchema))
            {
                if (!additionalPropertiesAllowed)
                {
                    context.Add(fieldPath, $"Unknown input property '{key}'.");
                }

                continue;
            }

            if (node.IsObject)
            {
                if (node.Children is Dictionary<string, ActionInputTreeNormalizer.NormalizedInputNode> nestedChildren)
                {
                    ValidateNormalizedObject(nestedChildren, propertySchema, fieldPath, context);
                }
            }
            else if (node.Leaf is not null)
            {
                ValidateValue(node.Leaf, propertySchema, fieldPath, context);
            }
        }

        foreach (var requiredKey in required.Where(requiredKey => !presentKeys.Contains(requiredKey)))
        {
            context.Add($"{jsonPath}.{requiredKey}", $"Required input property '{requiredKey}' is missing.");
        }
    }

    private static void ValidateValue(
        StateInputValueDefinition valueDef,
        JsonElement propertySchema,
        string jsonPath,
        ValidationContext context)
    {
        var valueKind = ResolveValueKind(propertySchema);
        if (valueDef.Path is not null)
        {
            ValidatePathValue(valueDef.Path, valueKind, jsonPath, context);
            return;
        }

        var literal = valueDef.Literal;
        if (literal is string literalString && IsPathExpression(literalString))
        {
            ValidatePathValue(literalString, valueKind, jsonPath, context);
            return;
        }

        if (valueKind == StateviaActionSchemaVocabulary.ValueKindPath)
        {
            context.Add(jsonPath, "A JSONPath expression is required for this field.");
            return;
        }

        if (valueKind == StateviaActionSchemaVocabulary.ValueKindLiteral
            && literal is string rejectedPath
            && IsPathExpression(rejectedPath))
        {
            context.Add(jsonPath, "JSONPath expressions are not allowed for this field.");
            return;
        }

        ValidateLiteral(literal, propertySchema, jsonPath, context);
    }

    private static void ValidatePathValue(
        string path,
        string valueKind,
        string jsonPath,
        ValidationContext context)
    {
        if (valueKind == StateviaActionSchemaVocabulary.ValueKindLiteral)
        {
            context.Add(jsonPath, "JSONPath expressions are not allowed for this field.");
            return;
        }

        if (!SimpleJsonPath.IsValid(path))
        {
            context.Add(jsonPath, $"Invalid JSONPath expression '{path}'.");
        }
    }

    private static void ValidateLiteral(
        object? literal,
        JsonElement propertySchema,
        string jsonPath,
        ValidationContext context)
    {
        if (propertySchema.TryGetProperty("oneOf", out var oneOf) && oneOf.ValueKind == JsonValueKind.Array)
        {
            if (!MatchesAnyOneOf(literal, oneOf, jsonPath, context))
            {
                context.Add(jsonPath, "Value does not match any allowed schema variant.");
            }

            return;
        }

        if (!propertySchema.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        if (!MatchesType(literal, typeElement, propertySchema))
        {
            var expected = DescribeType(typeElement);
            context.Add(jsonPath, $"Expected {expected}.");
        }
    }

    private static bool MatchesAnyOneOf(
        object? literal,
        JsonElement oneOf,
        string jsonPath,
        ValidationContext context)
    {
        foreach (var branch in oneOf.EnumerateArray())
        {
            var branchContext = new ValidationContext(context.State, context.ActionId);
            ValidateLiteral(literal, branch, jsonPath, branchContext);
            if (branchContext.Errors.Count == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesType(
        object? literal,
        JsonElement typeElement,
        JsonElement propertySchema)
    {
        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            return typeElement.EnumerateArray().Any(typeName => MatchesSingleType(literal, typeName, propertySchema));
        }

        return MatchesSingleType(literal, typeElement, propertySchema);
    }

    private static bool MatchesSingleType(object? literal, JsonElement typeName, JsonElement propertySchema)
    {
        if (typeName.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return typeName.GetString() switch
        {
            "string" => MatchesString(literal, propertySchema),
            "integer" => MatchesInteger(literal, propertySchema),
            "number" => MatchesNumber(literal),
            "boolean" => literal is bool,
            "object" => MatchesObjectLiteral(literal, propertySchema),
            "array" => literal is System.Collections.IList,
            "null" => literal is null,
            _ => false,
        };
    }

    private static bool MatchesString(object? literal, JsonElement propertySchema)
    {
        if (literal is not string text)
        {
            return false;
        }

        if (propertySchema.TryGetProperty("enum", out var enumElement)
            && enumElement.ValueKind == JsonValueKind.Array)
        {
            var allowed = enumElement.EnumerateArray()
                .Select(element => element.GetString())
                .ToHashSet(StringComparer.Ordinal);
            if (!allowed.Contains(text))
            {
                return false;
            }
        }

        if (propertySchema.TryGetProperty("format", out var formatElement)
            && formatElement.ValueKind == JsonValueKind.String)
        {
            return formatElement.GetString() switch
            {
                "uri" => Uri.TryCreate(text, UriKind.Absolute, out _),
                "email" => text.Contains('@', StringComparison.Ordinal)
                    && !text.StartsWith('@')
                    && !text.EndsWith('@'),
                _ => true,
            };
        }

        return true;
    }

    private static bool MatchesInteger(object? literal, JsonElement propertySchema)
    {
        if (!TryGetIntegralValue(literal, out var integral))
        {
            return false;
        }

        if (propertySchema.TryGetProperty("minimum", out var minimumElement)
            && minimumElement.TryGetInt64(out var minimum)
            && integral < minimum)
        {
            return false;
        }

        return true;
    }

    private static bool MatchesNumber(object? literal) =>
        literal is int or long or float or double or decimal;

    private static bool MatchesObjectLiteral(object? literal, JsonElement propertySchema)
    {
        if (literal is not IReadOnlyDictionary<string, object?> objectMap)
        {
            return false;
        }

        if (!propertySchema.TryGetProperty("additionalProperties", out var additionalProperties))
        {
            return true;
        }

        if (additionalProperties.ValueKind == JsonValueKind.False)
        {
            return objectMap.Count == 0;
        }

        if (additionalProperties.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        return objectMap.Values.All(value => value is null or string);
    }

    private static bool TryGetIntegralValue(object? literal, out long value)
    {
        switch (literal)
        {
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = longValue;
                return true;
            case double doubleValue:
                var truncated = Math.Truncate(doubleValue);
                if (Math.Abs(doubleValue - truncated) > 1e-9)
                {
                    value = 0;
                    return false;
                }

                value = Convert.ToInt64(truncated, CultureInfo.InvariantCulture);
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static string ResolveValueKind(JsonElement propertySchema)
    {
        if (propertySchema.TryGetProperty(ValueKindKeyword, out var valueKind)
            && valueKind.ValueKind == JsonValueKind.String
            && !string.IsNullOrWhiteSpace(valueKind.GetString()))
        {
            return valueKind.GetString()!;
        }

        return StateviaActionSchemaVocabulary.ValueKindLiteralOrPath;
    }

    private static bool TryGetProperties(
        JsonElement schema,
        out IReadOnlyDictionary<string, JsonElement>? properties)
    {
        if (!schema.TryGetProperty("properties", out var propertiesElement)
            || propertiesElement.ValueKind != JsonValueKind.Object)
        {
            properties = null;
            return false;
        }

        var map = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in propertiesElement.EnumerateObject())
        {
            map[property.Name] = property.Value;
        }

        properties = map;
        return true;
    }

    private static bool ResolveAdditionalProperties(JsonElement schema)
    {
        if (!schema.TryGetProperty("additionalProperties", out var additionalProperties))
        {
            return true;
        }

        return additionalProperties.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => true,
        };
    }

    private static string[] ResolveRequired(JsonElement schema)
    {
        if (!schema.TryGetProperty("required", out var requiredElement)
            || requiredElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return requiredElement.EnumerateArray()
            .Select(element => element.GetString())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray();
    }

    private static string DescribeType(JsonElement typeElement)
    {
        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            var names = typeElement.EnumerateArray()
                .Select(element => element.GetString())
                .Where(name => !string.IsNullOrWhiteSpace(name));
            return string.Join(" or ", names);
        }

        return typeElement.GetString() ?? "a supported type";
    }

    private static bool IsPathExpression(string value) =>
        value == "$" || value.StartsWith("$.", StringComparison.Ordinal);

    private sealed class ValidationContext
    {
        private readonly List<ActionInputValidationError> _errors = [];

        public ValidationContext(string state, string actionId)
        {
            State = state;
            ActionId = actionId;
        }

        public string State { get; }

        public string ActionId { get; }

        public List<ActionInputValidationError> Errors => _errors;

        public void Add(string jsonPath, string message) =>
            _errors.Add(new ActionInputValidationError(State, ActionId, jsonPath, message));
    }
}
