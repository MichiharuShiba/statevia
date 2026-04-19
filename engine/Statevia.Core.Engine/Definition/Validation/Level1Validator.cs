using Statevia.Core.Engine.Definition;

namespace Statevia.Core.Engine.Definition.Validation;

/// <summary>
/// レベル 1 検証：状態名・参照の整合性、自己遷移禁止、未定義状態参照の検出。
/// </summary>
public static class Level1Validator
{
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

        foreach (var (stateName, stateDef) in definition.States)
        {
            ValidateStateName(stateName, errors);
            ValidateActionAndWait(stateName, stateDef, errors);
            ValidateTransitions(stateName, stateDef, stateNames, errors);
            ValidateJoin(stateDef, stateNames, errors);
            ValidateStateInput(stateName, stateDef, errors);
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

    private static void ValidateTransitions(string stateName, StateDefinition stateDef, HashSet<string> stateNames, List<string> errors)
    {
        if (stateDef.On == null)
        {
            return;
        }

        foreach (var (_, trans) in stateDef.On)
        {
            ValidateNextTransition(stateName, trans, stateNames, errors);
        }
    }

    private static void ValidateNextTransition(string stateName, TransitionDefinition trans, HashSet<string> stateNames, List<string> errors)
    {
        ValidateTransitionTree(stateName, trans, stateNames, errors);
    }

    private static void ValidateTransitionTree(
        string stateName,
        TransitionDefinition? trans,
        HashSet<string> stateNames,
        List<string> errors)
    {
        if (trans is null)
        {
            return;
        }

        ValidateNextPointer(stateName, trans.Next, stateNames, errors);
        ValidateForkTransition(trans, stateNames, errors);
        ValidateTransitionTree(stateName, trans.Default, stateNames, errors);

        if (trans.Cases is null)
        {
            return;
        }

        foreach (var c in trans.Cases)
        {
            ValidateTransitionTree(stateName, c.Transition, stateNames, errors);
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

    private static void ValidateForkTransition(TransitionDefinition trans, HashSet<string> stateNames, List<string> errors)
    {
        if (trans.Fork == null)
        {
            return;
        }

        foreach (var forkState in trans.Fork.Where(fs => !stateNames.Contains(fs)))
        {
            errors.Add($"Fork references unknown state: {forkState}");
        }
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
