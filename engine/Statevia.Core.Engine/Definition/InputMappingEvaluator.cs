using System.Collections;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// inputMapping の最小評価器。JSONPath 風の path（$, $.a.b）を評価する。
/// </summary>
internal static class InputMappingEvaluator
{
    public static object? Apply(InputMappingDefinition? mapping, object? rawInput)
    {
        if (mapping == null || string.IsNullOrWhiteSpace(mapping.Path) || mapping.Path == "$")
        {
            return rawInput;
        }

        var path = mapping.Path!;
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
}
