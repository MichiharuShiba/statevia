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
    /// <param name="compiledTransition">評価対象のコンパイル済み遷移。</param>
    /// <param name="output">状態実行の output。</param>
    /// <param name="onPathWarning">path 解決警告の通知先（path, reason）。</param>
    public static TransitionResult Evaluate(
        CompiledFactTransition compiledTransition,
        object? output,
        Action<string, string>? onPathWarning = null)
    {
        if (compiledTransition.LinearTarget is { } linearTarget)
        {
            return ToTransitionResult(linearTarget);
        }

        foreach (var transitionCase in compiledTransition.Cases)
        {
            if (EvaluateConditionExpression(output, transitionCase.When, onPathWarning))
            {
                return ToTransitionResult(transitionCase.Target);
            }
        }

        if (compiledTransition.DefaultTarget is { } defaultTarget)
        {
            return ToTransitionResult(defaultTarget);
        }

        return TransitionResult.None;
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

    private static bool EvaluateConditionExpression(
        object? output,
        ConditionExpressionDefinition condition,
        Action<string, string>? onPathWarning)
    {
        var op = condition.Op.Trim().ToUpperInvariant();
        if (!TryResolvePath(output, condition.Path, out var actualValue, out var hasPath, onPathWarning))
        {
            return false;
        }

        return op switch
        {
            "EXISTS" => hasPath,
            "EQ" => hasPath && ValuesEqual(actualValue, condition.Value),
            "NE" => hasPath && !ValuesEqual(actualValue, condition.Value),
            "GT" => hasPath && TryCompare(actualValue, condition.Value, out var gt) && gt > 0,
            "GTE" => hasPath && TryCompare(actualValue, condition.Value, out var gte) && gte >= 0,
            "LT" => hasPath && TryCompare(actualValue, condition.Value, out var lt) && lt < 0,
            "LTE" => hasPath && TryCompare(actualValue, condition.Value, out var lte) && lte <= 0,
            "IN" => hasPath && TryEnumerate(condition.Value, out var inValues) && inValues.Any(candidate => ValuesEqual(actualValue, candidate)),
            "BETWEEN" => hasPath
                && TryEnumerate(condition.Value, out var rangeValues)
                && rangeValues.Count == 2
                && TryCompare(actualValue, rangeValues[0], out var lower)
                && TryCompare(actualValue, rangeValues[1], out var upper)
                && lower >= 0
                && upper <= 0,
            _ => false
        };
    }

    private static bool TryResolvePath(
        object? source,
        string path,
        out object? value,
        out bool found,
        Action<string, string>? onPathWarning)
    {
        value = null;
        found = false;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var resolve = SimpleJsonPathResolver.Resolve(source, path);
        if (!resolve.IsSupportedPathExpression)
        {
            if (resolve.WarningReason is not null)
            {
                onPathWarning?.Invoke(path, resolve.WarningReason);
            }

            return false;
        }

        if (resolve.WarningReason is not null)
        {
            onPathWarning?.Invoke(path, resolve.WarningReason);
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
