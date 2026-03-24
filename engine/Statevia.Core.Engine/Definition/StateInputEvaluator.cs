using System.Collections;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// <c>states.&lt;name&gt;.input</c> の最小評価器。JSONPath 風の path（$, $.a.b）を評価する。
/// </summary>
internal static class StateInputEvaluator
{
    public static object? Apply(StateInputDefinition? spec, object? rawInput)
    {
        if (spec == null)
        {
            return rawInput;
        }

        if (!string.IsNullOrWhiteSpace(spec.Path))
        {
            return EvaluatePath(spec.Path!, rawInput);
        }

        if (spec.Values == null || spec.Values.Count == 0)
        {
            return rawInput;
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, valueDef) in spec.Values)
        {
            var value = valueDef.Path != null ? EvaluatePath(valueDef.Path, rawInput) : valueDef.Literal;
            SetByDottedKey(result, key, value);
        }

        return result;
    }

    private static object? EvaluatePath(string path, object? rawInput)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "$")
        {
            return rawInput;
        }
        if (!path.StartsWith("$.", StringComparison.Ordinal))
        {
            return rawInput;
        }

        object? current = rawInput;
        var segments = path[2..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (current is IReadOnlyDictionary<string, object?> ro)
            {
                if (!ro.TryGetValue(segment, out current))
                {
                    return null;
                }
                continue;
            }
            if (current is IDictionary dict)
            {
                if (!dict.Contains(segment))
                {
                    return null;
                }
                current = dict[segment];
                continue;
            }
            return null;
        }
        return current;
    }

    private static void SetByDottedKey(Dictionary<string, object?> root, string dottedKey, object? value)
    {
        var parts = dottedKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        Dictionary<string, object?> current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            var p = parts[i];
            if (!current.TryGetValue(p, out var next) || next is not Dictionary<string, object?> nextDict)
            {
                nextDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                current[p] = nextDict;
            }
            current = nextDict;
        }
        current[parts[^1]] = value;
    }
}
