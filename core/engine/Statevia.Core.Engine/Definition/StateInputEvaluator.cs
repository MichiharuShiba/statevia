using Statevia.Core.Engine.Engine;

namespace Statevia.Core.Engine.Definition;

/// <summary>1 件の input 評価警告（同一 <see cref="StateInputEvaluator.ApplyWithDiagnostics"/> 呼び出し内の重複は抑制）。</summary>
internal readonly record struct StateInputWarning(string InputKey, string Reason);

/// <summary>input 評価の結果値と警告一覧。</summary>
internal sealed class StateInputEvaluationResult
{
    public object? Value { get; init; }
    public IReadOnlyList<StateInputWarning> Warnings { get; init; } = Array.Empty<StateInputWarning>();
}

/// <summary>
/// <c>states.&lt;name&gt;.input</c> の評価器。SimpleJsonPath（<c>$</c> / <c>$.a.b</c>）を
/// Execution Context 根で解決する。
/// </summary>
/// <remarks>
/// <para>
/// <c>input</c> 未定義時は候補 input（直前 output / Join 辞書など）をそのまま返す。
/// パス解決にレガシー rawInput フォールバックは持たない（execution-context Phase 1）。
/// </para>
/// </remarks>
internal static class StateInputEvaluator
{
    /// <summary>
    /// Context 根でパスを評価し、結果と警告を返す。
    /// </summary>
    /// <param name="spec">状態の input 定義。null のとき候補 input をそのまま返す。</param>
    /// <param name="context">評価根となる Execution Context。</param>
    /// <param name="candidateInput">input 未定義時に渡す候補（直前 output 等）。</param>
    public static StateInputEvaluationResult ApplyWithDiagnostics(
        StateInputDefinition? spec,
        WorkflowExecutionContext context,
        object? candidateInput)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (spec == null)
        {
            return new StateInputEvaluationResult { Value = candidateInput };
        }

        var warningList = new List<StateInputWarning>();
        var dedupe = new HashSet<(string Key, string Reason)>();

        if (!string.IsNullOrWhiteSpace(spec.Path))
        {
            var value = EvaluatePathWithDiagnostics(spec.Path!, context, spec.Path!, warningList, dedupe);
            return new StateInputEvaluationResult { Value = value, Warnings = warningList };
        }

        if (spec.Values == null || spec.Values.Count == 0)
        {
            return new StateInputEvaluationResult { Value = candidateInput };
        }

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, valueDef) in spec.Values)
        {
            if (valueDef.Path != null)
            {
                var value = EvaluatePathWithDiagnostics(valueDef.Path, context, key, warningList, dedupe);
                SetByDottedKey(result, key, value);
            }
            else
            {
                SetByDottedKey(result, key, valueDef.Literal);
            }
        }

        return new StateInputEvaluationResult { Value = result, Warnings = warningList };
    }

    /// <summary>診断付き評価の値のみを返す。</summary>
    public static object? Apply(
        StateInputDefinition? spec,
        WorkflowExecutionContext context,
        object? candidateInput) =>
        ApplyWithDiagnostics(spec, context, candidateInput).Value;

    private static object? EvaluatePathWithDiagnostics(
        string path,
        WorkflowExecutionContext context,
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

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var resolve = ExecutionContextPathResolver.Resolve(context, path);
        if (!resolve.IsSupportedPathExpression)
        {
            if (resolve.WarningReason is not null)
            {
                Warn(resolve.WarningReason);
            }

            return null;
        }

        if (resolve.WarningReason is not null)
        {
            Warn(resolve.WarningReason);
        }

        return resolve.Found ? resolve.Value : null;
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
