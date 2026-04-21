using System.Collections;
using System.Globalization;
using System.Text.Json;
using Statevia.Core.Engine.Abstractions;
using Statevia.Core.Engine.Definition;
using Statevia.Core.Engine.FSM;

namespace Statevia.Core.Engine.Engine;

/// <summary>
/// 条件遷移（cases/default）を評価して次の遷移結果を決定する。
/// </summary>
internal static class OutputConditionEvaluator
{
    /// <summary>
    /// コンパイル済みの事実遷移を output で評価し、遷移結果を返す。
    /// </summary>
    public static TransitionResult Evaluate(
        CompiledFactTransition compiledTransition,
        object? output,
        Action<string, string>? onPathWarning = null) =>
        EvaluateDetailed(compiledTransition, string.Empty, output, onPathWarning).Transition;

    /// <summary>
    /// コンパイル済みの事実遷移を評価し、遷移結果と観測用診断を返す。
    /// </summary>
    public static (TransitionResult Transition, ConditionRoutingDiagnostics Diagnostics) EvaluateDetailed(
        CompiledFactTransition compiledTransition,
        string fact,
        object? output,
        Action<string, string>? onPathWarning)
    {
        var evaluationErrors = new List<string>();

        if (compiledTransition.LinearTarget is { } linearTarget)
        {
            return (
                ToTransitionResult(linearTarget),
                new ConditionRoutingDiagnostics
                {
                    Fact = fact,
                    Resolution = "linear",
                    MatchedCaseIndex = null,
                    CaseEvaluations = Array.Empty<ConditionCaseEvaluationRecord>(),
                    EvaluationErrors = evaluationErrors
                });
        }

        var caseEvaluations = new List<ConditionCaseEvaluationRecord>();
        var caseIndex = 0;
        foreach (var transitionCase in compiledTransition.Cases)
        {
            var (matched, reasonCode, reasonDetail) = EvaluateConditionExpressionDetail(
                output,
                transitionCase.When,
                onPathWarning,
                evaluationErrors);

            caseEvaluations.Add(new ConditionCaseEvaluationRecord
            {
                CaseIndex = caseIndex,
                DeclarationIndex = transitionCase.DeclarationIndex,
                Order = transitionCase.Order,
                Matched = matched,
                ReasonCode = matched ? null : reasonCode,
                ReasonDetail = matched ? null : reasonDetail
            });

            if (matched)
            {
                return (
                    ToTransitionResult(transitionCase.Target),
                    new ConditionRoutingDiagnostics
                    {
                        Fact = fact,
                        Resolution = "matched_case",
                        MatchedCaseIndex = caseIndex,
                        CaseEvaluations = caseEvaluations,
                        EvaluationErrors = evaluationErrors
                    });
            }

            caseIndex++;
        }

        if (compiledTransition.DefaultTarget is { } defaultTarget)
        {
            return (
                ToTransitionResult(defaultTarget),
                new ConditionRoutingDiagnostics
                {
                    Fact = fact,
                    Resolution = "default_fallback",
                    MatchedCaseIndex = null,
                    CaseEvaluations = caseEvaluations,
                    EvaluationErrors = evaluationErrors
                });
        }

        evaluationErrors.Add("No case matched and compiled transition has no default target.");
        return (
            TransitionResult.None,
            new ConditionRoutingDiagnostics
            {
                Fact = fact,
                Resolution = "no_transition",
                MatchedCaseIndex = null,
                CaseEvaluations = caseEvaluations,
                EvaluationErrors = evaluationErrors
            });
    }

    private static TransitionResult ToTransitionResult(TransitionTarget transitionTarget)
    {
        if (transitionTarget.End)
        {
            return TransitionResult.ToEnd();
        }

        if (transitionTarget.Next is { } nextState)
        {
            return TransitionResult.ToNext(nextState);
        }

        if (transitionTarget.Fork is { Count: > 0 } forkTargets)
        {
            return TransitionResult.ToFork(forkTargets);
        }

        return TransitionResult.None;
    }

    /// <summary>
    /// 条件式の真偽と、偽のときの理由コードを返す。
    /// </summary>
    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateConditionExpressionDetail(
        object? output,
        ConditionExpressionDefinition condition,
        Action<string, string>? onPathWarning,
        List<string> evaluationErrors)
    {
        if (!ConditionExpressionOperatorNormalizer.TryNormalize(condition.Op, out var op))
        {
            var msg = $"Unsupported when.op: '{condition.Op}'.";
            evaluationErrors.Add(msg);
            return (false, "unsupported_op", msg);
        }

        if (!TryResolvePath(output, condition.Path, out var actualValue, out var hasPath, onPathWarning, evaluationErrors))
        {
            return (false, "path_resolution_failed", condition.Path);
        }

        return op switch
        {
            "EXISTS" => hasPath
                ? (true, null, null)
                : (false, "exists_absent", null),
            "EQ" => !hasPath
                ? (false, "path_not_found", null)
                : ValuesEqual(actualValue, condition.Value)
                    ? (true, null, null)
                    : (false, "condition_false", null),
            "NE" => !hasPath
                ? (false, "path_not_found", null)
                : !ValuesEqual(actualValue, condition.Value)
                    ? (true, null, null)
                    : (false, "condition_false", null),
            "GT" => !hasPath
                ? (false, "path_not_found", null)
                : EvaluateOrdered(actualValue, condition.Value, static c => c > 0),
            "GTE" => !hasPath
                ? (false, "path_not_found", null)
                : EvaluateOrdered(actualValue, condition.Value, static c => c >= 0),
            "LT" => !hasPath
                ? (false, "path_not_found", null)
                : EvaluateOrdered(actualValue, condition.Value, static c => c < 0),
            "LTE" => !hasPath
                ? (false, "path_not_found", null)
                : EvaluateOrdered(actualValue, condition.Value, static c => c <= 0),
            "IN" => !hasPath
                ? (false, "path_not_found", null)
                : EvaluateIn(actualValue, condition.Value),
            "BETWEEN" => !hasPath
                ? (false, "path_not_found", null)
                : EvaluateBetween(actualValue, condition.Value),
            _ => (false, "unsupported_op", op)
        };
    }

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateOrdered(
        object? actualValue,
        object? expectedValue,
        Func<int, bool> predicate)
    {
        if (actualValue is null || expectedValue is null)
        {
            return (false, "compare_operand_null", null);
        }

        if (!TryCompare(actualValue, expectedValue, out var comparison))
        {
            return (false, "compare_unsupported", null);
        }

        return predicate(comparison)
            ? (true, null, null)
            : (false, "condition_false", null);
    }

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateIn(
        object? actualValue,
        object? expectedValue)
    {
        if (!TryEnumerate(expectedValue, out var inValues))
        {
            return (false, "in_operand_not_collection", null);
        }

        return inValues.Any(candidate => ValuesEqual(actualValue, candidate))
            ? (true, null, null)
            : (false, "condition_false", null);
    }

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateBetween(
        object? actualValue,
        object? expectedValue)
    {
        if (!TryEnumerate(expectedValue, out var rangeValues) || rangeValues.Count != 2)
        {
            return (false, "between_operand_invalid", null);
        }

        if (!TryCompare(actualValue, rangeValues[0], out var lower))
        {
            return (false, "compare_unsupported", null);
        }

        if (!TryCompare(actualValue, rangeValues[1], out var upper))
        {
            return (false, "compare_unsupported", null);
        }

        return lower >= 0 && upper <= 0
            ? (true, null, null)
            : (false, "condition_false", null);
    }

    private static bool TryResolvePath(
        object? source,
        string path,
        out object? value,
        out bool found,
        Action<string, string>? onPathWarning,
        List<string> evaluationErrors)
    {
        value = null;
        found = false;

        if (string.IsNullOrWhiteSpace(path))
        {
            evaluationErrors.Add("when.path is empty or whitespace.");
            return false;
        }

        var resolve = SimpleJsonPathResolver.Resolve(source, path);
        if (!resolve.IsSupportedPathExpression)
        {
            if (resolve.WarningReason is not null)
            {
                onPathWarning?.Invoke(path, resolve.WarningReason);
                evaluationErrors.Add($"Path '{path}': {resolve.WarningReason}");
            }
            else
            {
                evaluationErrors.Add($"Path '{path}': unsupported path expression.");
            }

            return false;
        }

        if (resolve.WarningReason is not null)
        {
            onPathWarning?.Invoke(path, resolve.WarningReason);
            evaluationErrors.Add($"Path '{path}': {resolve.WarningReason}");
        }

        value = resolve.Value;
        found = resolve.Found;
        return true;
    }

    private static bool ValuesEqual(object? left, object? right)
    {
        left = NormalizeValue(left);
        right = NormalizeValue(right);

        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (TryConvertDecimal(left, out var leftNumber) && TryConvertDecimal(right, out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        return Equals(left, right);
    }

    private static bool TryCompare(object? left, object? right, out int comparison)
    {
        comparison = 0;
        left = NormalizeValue(left);
        right = NormalizeValue(right);
        if (left is null || right is null)
        {
            return false;
        }

        if (TryConvertDecimal(left, out var leftNumber) && TryConvertDecimal(right, out var rightNumber))
        {
            comparison = leftNumber.CompareTo(rightNumber);
            return true;
        }

        if (left is string leftString && right is string rightString)
        {
            comparison = string.Compare(leftString, rightString, StringComparison.Ordinal);
            return true;
        }

        if (left.GetType() == right.GetType() && left is IComparable comparable)
        {
            comparison = comparable.CompareTo(right);
            return true;
        }

        return false;
    }

    private static bool TryEnumerate(object? value, out List<object?> values)
    {
        values = new List<object?>();
        value = NormalizeValue(value);
        if (value is null || value is string || value is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (var item in enumerable)
        {
            values.Add(item);
        }

        return true;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is not JsonElement jsonElement)
        {
            return value;
        }

        return jsonElement.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => jsonElement.GetString(),
            JsonValueKind.Number when jsonElement.TryGetInt64(out var l) => l,
            JsonValueKind.Number when jsonElement.TryGetDouble(out var d) => d,
            _ => jsonElement
        };
    }

    private static bool TryConvertDecimal(object value, out decimal result)
    {
        switch (value)
        {
            case byte b:
                result = b;
                return true;
            case sbyte sb:
                result = sb;
                return true;
            case short s:
                result = s;
                return true;
            case ushort us:
                result = us;
                return true;
            case int i:
                result = i;
                return true;
            case uint ui:
                result = ui;
                return true;
            case long l:
                result = l;
                return true;
            case ulong ul:
                result = ul;
                return true;
            case float f:
                result = (decimal)f;
                return true;
            case double db:
                result = (decimal)db;
                return true;
            case decimal dec:
                result = dec;
                return true;
            case string s when decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                result = parsed;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
