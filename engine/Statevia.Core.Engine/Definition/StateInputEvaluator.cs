using System.Collections;

namespace Statevia.Core.Engine.Definition;

/// <summary>
/// <c>StateInputEvaluator</c> の Warning ログへ渡す理由文字列（STV-405）。
/// 条件の基準をコード上で固定する。
/// </summary>
internal static class StateInputWarningReasons
{
    /// <summary>path が <c>$.</c> で始まらず raw input をそのまま渡すフォールバック。</summary>
    public const string IgnoredNonDollarDotPath = "IgnoredNonDollarDotPath";

    /// <summary>JSONPath 風セグメントが辞書に存在しない。</summary>
    public const string PathSegmentMissing = "PathSegmentMissing";

    /// <summary>中間値がマッピング型ではなくトラバース不能。</summary>
    public const string PathTraversalNotMapping = "PathTraversalNotMapping";
}

/// <summary>1 件の input 評価警告（同一 <see cref="ApplyWithDiagnostics"/> 呼び出し内の重複は抑制）。</summary>
internal readonly record struct StateInputWarning(string InputKey, string Reason);

/// <summary>input 評価の結果値と警告一覧。</summary>
internal sealed class StateInputEvaluationResult
{
    public object? Value { get; init; }
    public IReadOnlyList<StateInputWarning> Warnings { get; init; } = Array.Empty<StateInputWarning>();
}

/// <summary>
/// <c>states.&lt;name&gt;.input</c> の最小評価器。JSONPath 風の path（$, $.a.b）を評価する。
/// </summary>
internal static class StateInputEvaluator
{
    /// <summary>
    /// 評価結果と、注意が必要だった経路の警告を返す（STV-405）。
    /// </summary>
    public static StateInputEvaluationResult ApplyWithDiagnostics(StateInputDefinition? spec, object? rawInput)
    {
        if (spec == null)
        {
            return new StateInputEvaluationResult { Value = rawInput };
        }

        var warningList = new List<StateInputWarning>();
        var dedupe = new HashSet<(string Key, string Reason)>();

        if (!string.IsNullOrWhiteSpace(spec.Path))
        {
            var value = EvaluatePathWithDiagnostics(spec.Path!, rawInput, spec.Path!, warningList, dedupe);
            return new StateInputEvaluationResult { Value = value, Warnings = warningList };
        }

        if (spec.Values == null || spec.Values.Count == 0)
        {
            return new StateInputEvaluationResult { Value = rawInput };
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, valueDef) in spec.Values)
        {
            if (valueDef.Path != null)
            {
                var value = EvaluatePathWithDiagnostics(valueDef.Path, rawInput, key, warningList, dedupe);
                SetByDottedKey(result, key, value);
            }
            else
            {
                SetByDottedKey(result, key, valueDef.Literal);
            }
        }

        return new StateInputEvaluationResult { Value = result, Warnings = warningList };
    }

    public static object? Apply(StateInputDefinition? spec, object? rawInput) =>
        ApplyWithDiagnostics(spec, rawInput).Value;

    private static object? EvaluatePathWithDiagnostics(
        string path,
        object? rawInput,
        string inputKey,
        List<StateInputWarning> warnings,
        HashSet<(string Key, string Reason)> dedupe)
    {
        void Warn(string reason)
        {
            if (dedupe.Add((inputKey, reason)))
            {
                warnings.Add(new StateInputWarning(inputKey, reason));
            }
        }

        if (string.IsNullOrWhiteSpace(path) || path == "$")
        {
            return rawInput;
        }

        if (!path.StartsWith("$.", StringComparison.Ordinal))
        {
            Warn(StateInputWarningReasons.IgnoredNonDollarDotPath);
            return rawInput;
        }

        object? current = rawInput;
        var segments = path[2..].Split('.', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (current is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                if (!readOnlyDictionary.TryGetValue(segment, out current))
                {
                    Warn(StateInputWarningReasons.PathSegmentMissing);
                    return null;
                }
                continue;
            }

            if (current is IDictionary dictionary)
            {
                if (!dictionary.Contains(segment))
                {
                    Warn(StateInputWarningReasons.PathSegmentMissing);
                    return null;
                }
                current = dictionary[segment]!;
                continue;
            }

            Warn(StateInputWarningReasons.PathTraversalNotMapping);
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
