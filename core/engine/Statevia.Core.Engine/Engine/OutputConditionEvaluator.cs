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
/// <remarks>
/// <c>when.path</c> の評価根は <see cref="WorkflowExecutionContext"/>（Execution Context）である。
/// State output 直下を根とする旧記法は採用しない。
/// </remarks>
internal static class OutputConditionEvaluator
{
    private const string ReasonUnsupportedOp = "unsupported_op";
    private const string ReasonPathResolutionFailed = "path_resolution_failed";
    private const string ReasonPathNotFound = "path_not_found";
    private const string ReasonConditionFalse = "condition_false";
    private const string ReasonExistsAbsent = "exists_absent";
    private const string ReasonCompareOperandNull = "compare_operand_null";
    private const string ReasonCompareUnsupported = "compare_unsupported";
    private const string ReasonInOperandNotCollection = "in_operand_not_collection";
    private const string ReasonBetweenOperandInvalid = "between_operand_invalid";

    /// <summary>
    /// コンパイル済みの事実遷移を Execution Context で評価し、遷移結果を返す。
    /// </summary>
    /// <param name="compiledTransition">コンパイル済み事実遷移。</param>
    /// <param name="context">パス評価根（Execution Context）。</param>
    /// <param name="onPathWarning">パス警告コールバック。</param>
    /// <returns>遷移結果。</returns>
    public static TransitionResult Evaluate(
        CompiledFactTransition compiledTransition,
        WorkflowExecutionContext context,
        Action<string, string>? onPathWarning = null) =>
        EvaluateDetailed(compiledTransition, string.Empty, context, onPathWarning).Transition;

    /// <summary>
    /// コンパイル済みの事実遷移を評価し、遷移結果と観測用診断を返す。
    /// </summary>
    /// <param name="compiledTransition">コンパイル済み事実遷移。</param>
    /// <param name="fact">評価対象の事実名。</param>
    /// <param name="context">パス評価根（Execution Context）。</param>
    /// <param name="onPathWarning">パス警告コールバック。</param>
    /// <returns>遷移結果と診断。</returns>
    public static (TransitionResult Transition, ConditionRoutingDiagnostics Diagnostics) EvaluateDetailed(
        CompiledFactTransition compiledTransition,
        string fact,
        WorkflowExecutionContext context,
        Action<string, string>? onPathWarning)
    {
        ArgumentNullException.ThrowIfNull(context);
        var evaluationErrors = new List<string>();

        // Context スナップショットは評価単位で 1 回だけ取得して全 case で使い回す。
        // これにより sys（now/today など）が case 間で一貫し、全 case 分の再確保も避ける。
        var root = context.ToPathRoot();

        if (compiledTransition.LinearTarget is { } linearTarget)
        {
            return (
                ToTransitionResult(linearTarget),
                new ConditionRoutingDiagnostics
                {
                    Fact = fact,
                    Resolution = ConditionRoutingResolutions.Linear,
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
                root,
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
                        Resolution = ConditionRoutingResolutions.MatchedCase,
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
                    Resolution = ConditionRoutingResolutions.DefaultFallback,
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
                Resolution = ConditionRoutingResolutions.NoTransition,
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
        IReadOnlyDictionary<string, object?> root,
        ConditionExpressionDefinition condition,
        Action<string, string>? onPathWarning,
        List<string> evaluationErrors)
    {
        if (!ConditionExpressionOperatorNormalizer.TryNormalize(condition.Op, out var op))
        {
            var msg = $"Unsupported when.op: '{condition.Op}'.";
            evaluationErrors.Add(msg);
            return (false, ReasonUnsupportedOp, msg);
        }

        if (!TryResolvePath(root, condition.Path, out var actualValue, out var hasPath, onPathWarning, evaluationErrors))
        {
            return (false, ReasonPathResolutionFailed, condition.Path);
        }

        return EvaluateNormalizedOperator(op, hasPath, actualValue, condition.Value);
    }

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateNormalizedOperator(
        string op,
        bool hasPath,
        object? actualValue,
        object? expectedValue) =>
        op switch
        {
            "EXISTS" => EvaluateExists(hasPath),
            "EQ" => EvaluateEq(hasPath, actualValue, expectedValue),
            "NE" => EvaluateNe(hasPath, actualValue, expectedValue),
            "GT" => EvaluateOrderedWhenPathPresent(hasPath, actualValue, expectedValue, static c => c > 0),
            "GTE" => EvaluateOrderedWhenPathPresent(hasPath, actualValue, expectedValue, static c => c >= 0),
            "LT" => EvaluateOrderedWhenPathPresent(hasPath, actualValue, expectedValue, static c => c < 0),
            "LTE" => EvaluateOrderedWhenPathPresent(hasPath, actualValue, expectedValue, static c => c <= 0),
            "IN" => EvaluateInWhenPathPresent(hasPath, actualValue, expectedValue),
            "BETWEEN" => EvaluateBetweenWhenPathPresent(hasPath, actualValue, expectedValue),
            _ => (false, ReasonUnsupportedOp, op)
        };

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateExists(bool hasPath) =>
        hasPath ? (true, null, null) : (false, ReasonExistsAbsent, null);

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateEq(
        bool hasPath,
        object? actualValue,
        object? expectedValue)
    {
        if (!hasPath)
            return (false, ReasonPathNotFound, null);
        if (ValuesEqual(actualValue, expectedValue))
            return (true, null, null);
        return (false, ReasonConditionFalse, null);
    }

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateNe(
        bool hasPath,
        object? actualValue,
        object? expectedValue)
    {
        if (!hasPath)
            return (false, ReasonPathNotFound, null);
        if (!ValuesEqual(actualValue, expectedValue))
            return (true, null, null);
        return (false, ReasonConditionFalse, null);
    }

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateOrderedWhenPathPresent(
        bool hasPath,
        object? actualValue,
        object? expectedValue,
        Func<int, bool> predicate) =>
        !hasPath ? (false, ReasonPathNotFound, null) : EvaluateOrdered(actualValue, expectedValue, predicate);

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateInWhenPathPresent(
        bool hasPath,
        object? actualValue,
        object? expectedValue) =>
        !hasPath ? (false, ReasonPathNotFound, null) : EvaluateIn(actualValue, expectedValue);

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateBetweenWhenPathPresent(
        bool hasPath,
        object? actualValue,
        object? expectedValue) =>
        !hasPath ? (false, ReasonPathNotFound, null) : EvaluateBetween(actualValue, expectedValue);

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateOrdered(
        object? actualValue,
        object? expectedValue,
        Func<int, bool> predicate)
    {
        if (actualValue is null || expectedValue is null)
        {
            return (false, ReasonCompareOperandNull, null);
        }

        if (!TryCompare(actualValue, expectedValue, out var comparison))
        {
            return (false, ReasonCompareUnsupported, null);
        }

        return predicate(comparison)
            ? (true, null, null)
            : (false, ReasonConditionFalse, null);
    }

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateIn(
        object? actualValue,
        object? expectedValue)
    {
        if (!TryEnumerate(expectedValue, out var inValues))
        {
            return (false, ReasonInOperandNotCollection, null);
        }

        return inValues.Any(candidate => ValuesEqual(actualValue, candidate))
            ? (true, null, null)
            : (false, ReasonConditionFalse, null);
    }

    private static (bool Matched, string? ReasonCode, string? ReasonDetail) EvaluateBetween(
        object? actualValue,
        object? expectedValue)
    {
        if (!TryEnumerate(expectedValue, out var rangeValues) || rangeValues.Count != 2)
        {
            return (false, ReasonBetweenOperandInvalid, null);
        }

        if (!TryCompare(actualValue, rangeValues[0], out var lower))
        {
            return (false, ReasonCompareUnsupported, null);
        }

        if (!TryCompare(actualValue, rangeValues[1], out var upper))
        {
            return (false, ReasonCompareUnsupported, null);
        }

        return lower >= 0 && upper <= 0
            ? (true, null, null)
            : (false, ReasonConditionFalse, null);
    }

    private static bool TryResolvePath(
        IReadOnlyDictionary<string, object?> root,
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

        var resolve = ExecutionContextPathResolver.ResolveWithRoot(root, path);
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

        if (TryEqualityWithBooleanCoercion(left, right, out var boolEquality))
        {
            return boolEquality;
        }

        if (TryConvertDecimal(left, out var leftNumber) && TryConvertDecimal(right, out var rightNumber))
        {
            return leftNumber == rightNumber;
        }

        return Equals(left, right);
    }

    /// <summary>
    /// YAML / JSON で片側のみ真偽に解釈されるスカラーが混ざる場合（例: 実側が長整数 1、定義側が <c>true</c>）。
    /// いずれかが <see cref="bool"/> のときにもう一方を真偽に正規化して比較する。
    /// </summary>
    private static bool TryEqualityWithBooleanCoercion(object left, object right, out bool equal)
    {
        equal = false;

        if (left is bool leftBool && TryCoerceToBoolForMixedComparison(right, out var rightBool))
        {
            equal = leftBool == rightBool;
            return true;
        }

        if (right is bool rightBool2 && TryCoerceToBoolForMixedComparison(left, out var leftBool2))
        {
            equal = leftBool2 == rightBool2;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 条件式で <c>true</c> / <c>false</c> と比較される列挙のみを真偽に写像する。曖昧な数値や文字列は失敗させる。
    /// </summary>
    private static bool TryCoerceToBoolForMixedComparison(object value, out bool converted)
    {
        if (value is bool b)
        {
            converted = b;
            return true;
        }

        if (value is string s)
            return TryCoerceStringToBool(s, out converted);

        if (TryCoerceIntegralLikeNumberToBool(value, out converted))
            return true;

        if (value is float f && ApproxBinaryFloat(f))
        {
            converted = Math.Abs(f - 1f) < 1e-6f;
            return true;
        }

        if (value is double db && ApproxBinaryDouble(db))
        {
            converted = Math.Abs(db - 1d) < 1e-9;
            return true;
        }

        converted = default;
        return false;
    }

    private static bool TryCoerceStringToBool(string raw, out bool converted)
    {
        var trimmed = raw.Trim();
        if (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase))
        {
            converted = true;
            return true;
        }

        if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
        {
            converted = false;
            return true;
        }

        if (string.Equals(trimmed, "1", StringComparison.Ordinal))
        {
            converted = true;
            return true;
        }

        if (string.Equals(trimmed, "0", StringComparison.Ordinal))
        {
            converted = false;
            return true;
        }

        converted = default;
        return false;
    }

    private static bool TryCoerceIntegralLikeNumberToBool(object value, out bool converted)
    {
        converted = default;
        switch (value)
        {
            case byte b when b is 0 or 1:
                converted = b != 0;
                return true;
            case sbyte sb when sb is 0 or 1:
                converted = sb != 0;
                return true;
            case short sh when sh is 0 or 1:
                converted = sh != 0;
                return true;
            case ushort ush when ush is 0 or 1:
                converted = ush != 0;
                return true;
            case int i when i is 0 or 1:
                converted = i != 0;
                return true;
            case uint ui when ui is 0 or 1:
                converted = ui != 0;
                return true;
            case long l when l is 0L or 1L:
                converted = l != 0;
                return true;
            case ulong ul when ul is 0UL or 1UL:
                converted = ul != 0;
                return true;
            case decimal dcm when dcm == 0m || dcm == 1m:
                converted = dcm != 0m;
                return true;
            default:
                return false;
        }
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
        values = [];
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

    private static bool ApproxBinaryFloat(float value) =>
        float.IsFinite(value) && (Math.Abs(value) < 1e-6f || Math.Abs(value - 1f) < 1e-6f);

    private static bool ApproxBinaryDouble(double value) =>
        double.IsFinite(value) && (Math.Abs(value) < 1e-9 || Math.Abs(value - 1d) < 1e-9);

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
