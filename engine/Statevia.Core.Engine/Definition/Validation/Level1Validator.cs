using System.Collections;
using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// レベル 1 検証：状態名・参照の整合性、自己遷移禁止、未定義状態参照の検出。
/// </summary>
public static class Level1Validator
{
    private static readonly HashSet<string> SupportedConditionOperators =
    [
        "EQ",
        "NE",
        "GT",
        "GTE",
        "LT",
        "LTE",
        "EXISTS",
        "IN",
        "BETWEEN"
    ];

    /// <summary>ワークフロー定義を検証し、エラー一覧を返します。</summary>
    public static ValidationResult Validate(WorkflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();
        if (definition.States.Count == 0)
        {
            errors.Add("At least one state is required.");
            return new ValidationResult(errors);
        }

        var stateNames = new HashSet<string>(definition.States.Keys, StringComparer.OrdinalIgnoreCase);
        var terminalTransitionCount = 0;

        foreach (var (stateName, stateDef) in definition.States)
        {
            ValidateStateName(stateName, errors);
            ValidateActionAndWait(stateName, stateDef, errors);
            ValidateTransitions(stateName, stateDef, stateNames, errors, ref terminalTransitionCount);
            ValidateJoin(stateDef, stateNames, errors);
            ValidateStateInput(stateName, stateDef, errors);
        }

        if (terminalTransitionCount == 0)
        {
            errors.Add("At least one terminal transition (end: true) is required.");
        }

        return new ValidationResult(errors);
    }

    private static void ValidateStateName(string stateName, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(stateName))
        {
            errors.Add("State name cannot be empty.");
        }
    }

    private static void ValidateActionAndWait(string stateName, StateDefinition stateDef, List<string> errors)
    {
        if (stateDef.Wait != null && !string.IsNullOrWhiteSpace(stateDef.Action))
        {
            errors.Add($"State '{stateName}' cannot specify both wait and action.");
        }
    }

    private static void ValidateTransitions(
        string stateName,
        StateDefinition stateDef,
        HashSet<string> stateNames,
        List<string> errors,
        ref int terminalTransitionCount)
    {
        if (stateDef.On == null)
        {
            return;
        }

        foreach (var (fact, trans) in stateDef.On)
        {
            ValidateTransitionTree(
                stateName,
                $"on.{fact}",
                trans,
                stateNames,
                errors,
                isDefaultTransition: false,
                ref terminalTransitionCount);
        }
    }

    private static void ValidateTransitionTree(
        string stateName,
        string transitionPath,
        TransitionDefinition? trans,
        HashSet<string> stateNames,
        List<string> errors,
        bool isDefaultTransition,
        ref int terminalTransitionCount)
    {
        if (trans is null)
        {
            return;
        }

        var hasNext = !string.IsNullOrWhiteSpace(trans.Next);
        var hasFork = trans.Fork is { Count: > 0 };
        var hasEnd = trans.End;
        var hasCases = trans.Cases is { Count: > 0 };
        var hasDefault = trans.Default is not null;
        var usesLinearForm = hasNext || trans.Fork is not null || hasEnd;
        var usesConditionalForm = trans.Cases is not null || hasDefault;
        var linearCount = (hasNext ? 1 : 0) + (hasFork ? 1 : 0) + (hasEnd ? 1 : 0);

        if (hasEnd)
        {
            terminalTransitionCount++;
        }

        if (trans.Fork is { Count: 0 })
        {
            errors.Add($"Transition '{transitionPath}' has empty fork target list.");
        }

        if (trans.Cases is { Count: 0 })
        {
            errors.Add($"Transition '{transitionPath}' has empty cases list.");
        }

        if (isDefaultTransition)
        {
            if (usesConditionalForm)
            {
                errors.Add($"Transition '{transitionPath}' must not include cases/default inside default transition.");
            }

            if (linearCount != 1)
            {
                errors.Add($"Transition '{transitionPath}' must define exactly one of next/fork/end.");
            }
        }
        else
        {
            if (usesLinearForm && usesConditionalForm)
            {
                errors.Add($"Transition '{transitionPath}' cannot mix next/fork/end with cases/default.");
            }

            if (!usesLinearForm && !usesConditionalForm)
            {
                errors.Add($"Transition '{transitionPath}' must define next/fork/end or cases/default.");
            }

            if (usesLinearForm && !usesConditionalForm && linearCount != 1)
            {
                errors.Add($"Transition '{transitionPath}' must define exactly one of next/fork/end.");
            }

            if (hasCases && !hasDefault)
            {
                errors.Add($"Transition '{transitionPath}' requires default when cases are defined.");
            }

            if (!hasCases && hasDefault)
            {
                errors.Add($"Transition '{transitionPath}' cannot define default without cases.");
            }

            if (hasEnd && (hasNext || hasFork))
            {
                errors.Add($"Transition '{transitionPath}' cannot combine end: true with next/fork.");
            }
        }

        if (hasNext)
        {
            ValidateNextPointer(stateName, trans.Next, stateNames, errors);
        }

        if (trans.Fork is not null)
        {
            ValidateForkTransition(transitionPath, trans.Fork, stateNames, errors);
        }

        if (trans.Cases is not null)
        {
            for (var i = 0; i < trans.Cases.Count; i++)
            {
                var transitionCase = trans.Cases[i];
                ValidateCondition(transitionPath, i, transitionCase.When, errors);
                ValidateTransitionTree(
                    stateName,
                    $"{transitionPath}.cases[{i}]",
                    transitionCase.Transition,
                    stateNames,
                    errors,
                    isDefaultTransition: false,
                    ref terminalTransitionCount);
            }
        }

        if (trans.Default is not null)
        {
            ValidateTransitionTree(
                stateName,
                $"{transitionPath}.default",
                trans.Default,
                stateNames,
                errors,
                isDefaultTransition: true,
                ref terminalTransitionCount);
        }
    }

    private static void ValidateNextPointer(string stateName, string? next, HashSet<string> stateNames, List<string> errors)
    {
        if (next == null)
        {
            return;
        }

        if (next.Equals(stateName, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Self-transition not allowed: {stateName} -> {stateName}");
        }

        if (!stateNames.Contains(next))
        {
            errors.Add($"Reference to unknown state: {next}");
        }
    }

    private static void ValidateForkTransition(
        string transitionPath,
        IReadOnlyList<string> forkStates,
        HashSet<string> stateNames,
        List<string> errors)
    {
        foreach (var forkState in forkStates.Where(fs => !stateNames.Contains(fs)))
        {
            errors.Add($"Fork references unknown state: {forkState} (transition '{transitionPath}')");
        }
    }

    private static void ValidateCondition(
        string transitionPath,
        int caseIndex,
        ConditionExpressionDefinition condition,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(condition.Path) || !SimpleJsonPath.IsValid(condition.Path))
        {
            errors.Add($"Transition '{transitionPath}.cases[{caseIndex}]' has invalid when.path: {condition.Path}");
        }

        if (string.IsNullOrWhiteSpace(condition.Op))
        {
            errors.Add($"Transition '{transitionPath}.cases[{caseIndex}]' requires non-empty when.op.");
            return;
        }

        var op = condition.Op.Trim().ToUpperInvariant();
        if (!SupportedConditionOperators.Contains(op))
        {
            errors.Add(
                $"Transition '{transitionPath}.cases[{caseIndex}]' has unsupported when.op: '{condition.Op}'.");
            return;
        }

        switch (op)
        {
            case "EXISTS":
                if (condition.Value is not null)
                {
                    errors.Add(
                        $"Transition '{transitionPath}.cases[{caseIndex}]' with op 'exists' must not define value.");
                }

                break;
            case "BETWEEN":
                if (!TryGetCollectionItems(condition.Value, out var range) || range.Count != 2)
                {
                    errors.Add($"Transition '{transitionPath}.cases[{caseIndex}]' with op 'between' requires two-element array value.");
                }

                break;
            case "IN":
                if (!TryGetCollectionItems(condition.Value, out _))
                {
                    errors.Add($"Transition '{transitionPath}.cases[{caseIndex}]' with op 'in' requires array value.");
                }

                break;
        }
    }

    private static bool TryGetCollectionItems(object? value, out List<object?> items)
    {
        items = new List<object?>();
        if (value is null || value is string || value is not IEnumerable enumerable)
        {
            return false;
        }

        foreach (var item in enumerable)
        {
            items.Add(item);
        }

        return true;
    }

    private static void ValidateJoin(StateDefinition stateDef, HashSet<string> stateNames, List<string> errors)
    {
        if (stateDef.Join == null)
        {
            return;
        }

        foreach (var joinState in stateDef.Join.AllOf.Where(js => !stateNames.Contains(js)))
        {
            errors.Add($"Join references unknown state: {joinState}");
        }
    }

    private static void ValidateStateInput(string stateName, StateDefinition stateDef, List<string> errors)
    {
        var m = stateDef.Input;
        if (m == null)
        {
            return;
        }

        if (m.Path != null)
        {
            if (!SimpleJsonPath.IsValid(m.Path))
            {
                errors.Add($"input.path is invalid for state '{stateName}': {m.Path}");
            }
            return;
        }

        if (m.Values == null || m.Values.Count == 0)
        {
            errors.Add($"input must define path or values: {stateName}");
            return;
        }

        foreach (var (key, valueDef) in m.Values)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                errors.Add($"input key cannot be empty: {stateName}");
                continue;
            }

            if (valueDef.Path != null && !SimpleJsonPath.IsValid(valueDef.Path))
            {
                errors.Add($"input.path is invalid for state '{stateName}' key '{key}': {valueDef.Path}");
            }
        }
    }

}

/// <summary>検証結果。エラー一覧と有効フラグを保持します。</summary>
public sealed class ValidationResult
{
    /// <summary>検出されたエラーメッセージの一覧。</summary>
    public IReadOnlyList<string> Errors { get; }
    /// <summary>エラーが 0 件の場合 true。</summary>
    public bool IsValid => Errors.Count == 0;
    public ValidationResult(IReadOnlyList<string> errors) => Errors = errors;
}
